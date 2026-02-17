#!/usr/bin/env python3

import os
import re
import shlex
import shutil
import json
import subprocess
import sys
import tempfile
from io import StringIO, TextIOBase
from pathlib import Path
from typing import Optional, List, Tuple
from dataclasses import dataclass

@dataclass
class CommitInfo:
	sha: str
	short_sha: str
	message_title: str
	html_url: str
	author_name: str
	date: str


@dataclass
class PullRequestInfo:
	number: int
	title: str
	html_url: str
	author: str


def run(cmd: List[str], capture_output: bool = False) -> subprocess.CompletedProcess:
	return subprocess.run(cmd, capture_output=capture_output, text=True, check=True)

def get_var_from_file(file: Path, var: str) -> Optional[str]:
	with file.open("r", encoding="utf-8") as f:
		return get_var_from_stream(f, var)

def get_var_from_str(input: str, var: str) -> Optional[str]:
	with StringIO(input) as stream:
		return get_var_from_stream(stream, var)

def get_var_from_stream(stream: TextIOBase, var: str) -> Optional[str]:
	pattern = re.compile(rf'^\s*{re.escape(var)}\s*=\s*(.*)')
	for line in stream:
		m = pattern.match(line)
		if m:
			val = m.group(1).strip()
			# remove trailing comments
			val = re.split(r'\s+#', val, 1)[0].strip()
			# strip matching surrounding quotes
			if (val.startswith('"') and val.endswith('"')) or (val.startswith("'") and val.endswith("'")):
				val = val[1:-1]
			return val
	return None


def parse_github_repo(url: str) -> Tuple[str, str]:
	# Accepts URLs like https://github.com/owner/repo(.git)? or git@github.com:owner/repo.git
	m = re.match(r"(?:https?://github\.com/|git@github\.com:)([^/ :]+/[^/ :]+)(?:\.git)?", url)
	if not m:
		raise ValueError("Failed to parse owner/repo from URL")
	owner_repo = m.group(1).rstrip(".git")
	owner, repo = owner_repo.split("/", 1)
	return owner, repo


def write_tmp_and_replace(path: Path, text: str) -> None:
	# Ensure exactly one trailing newline
	if not text.endswith("\n"):
		text += "\n"

	path.parent.mkdir(parents=True, exist_ok=True)

	with tempfile.NamedTemporaryFile(dir=str(path.parent), prefix=path.name + ".", mode="w", encoding="utf-8", newline="\n", delete=False) as tmp:
		tmp.write(text)
		tmp.flush()
		os.fsync(tmp.fileno())
		tmp_path = Path(tmp.name)

	try:
		# pathlib.Path.replace performs an atomic rename when possible
		tmp_path.replace(path)
	except Exception:
		# cleanup temp file on error
		try:
			tmp_path.unlink(missing_ok=True)
		except Exception:
			pass
		raise


def github_api_get(url: str, gh_token: str = "") -> dict:
	"""Call GitHub API via curl and return parsed JSON (fail-fast on errors)."""
	cmd = ["curl", "-sk", "-H", "Accept: application/vnd.github.v3+json"]
	if gh_token:
		cmd += ["-H", f"Authorization: token {gh_token}"]
	cmd += [url]
	
	# run and capture output
	proc = subprocess.run(cmd, capture_output=True, text=True, check=False)
	if proc.returncode != 0:
		raise RuntimeError(f"curl failed ({proc.returncode}): {proc.stdout.strip()}")
	if proc.stdout.strip() == "":
		raise RuntimeError("Empty response from GitHub API")

	try:
		return json.loads(proc.stdout)
	except json.JSONDecodeError as e:
		raise RuntimeError(f"Failed to parse JSON from GitHub API: {e}") from e


def get_commits_between(owner: str, repo: str, from_sha: str, to_sha: str, gh_token: str = "") -> List[CommitInfo]:
	"""
	Use GitHub API to list commits between from_sha (exclusive) and to_sha (inclusive).
	Returns commits ordered newest->oldest as CommitInfo objects.
	"""
	# GitHub compare API: /repos/{owner}/{repo}/compare/{base}...{head}
	compare_api = f"https://api.github.com/repos/{owner}/{repo}/compare/{from_sha}...{to_sha}"
	data = github_api_get(compare_api, gh_token)
	if "commits" not in data:
		raise RuntimeError(f"Compare API error: {data.get('message', 'no commits field')}")

	# Parse commits, but fail-fast on unexpected/missing structure
	commits = []
	try:
		for raw_commit in data["commits"]:
			sha = raw_commit["sha"]
			short = sha[:7]
			commit_obj = raw_commit["commit"]
			msg = commit_obj["message"].splitlines()[0]
			author_obj = commit_obj["author"]
			date = author_obj["date"]
			author = author_obj.get("name", "")
			html_url = raw_commit.get("html_url") or f"https://github.com/{owner}/{repo}/commit/{sha}"
			commits.append(CommitInfo(
				sha=sha,
				short_sha=short,
				message_title=msg,
				html_url=html_url,
				author_name=author,
				date=date,
			))
	except (KeyError, TypeError) as e:
		raise RuntimeError(f"Unexpected compare API response structure: {e}") from e

	return commits


def commit_prs(owner: str, repo: str, sha: str, gh_token: str = "") -> List[PullRequestInfo]:
	"""Return list of PullRequestInfo for a given commit SHA using the commit->pulls endpoint."""
	url = f"https://api.github.com/repos/{owner}/{repo}/commits/{sha}/pulls"
	raw = github_api_get(url, gh_token)
	prs: List[PullRequestInfo] = []
	try:
		for item in raw:
			prs.append(PullRequestInfo(
				number=int(item["number"]),
				title=item["title"].strip(),
				html_url=item["html_url"],
				author=item["user"]["login"]
			))
	except (KeyError, TypeError) as e:
		raise RuntimeError(f"Unexpected commits API response structure: {e}") from e
	return prs


def aggregate_prs_for_commits(owner: str, repo: str, commits: List[CommitInfo], gh_token: str = "") -> List[PullRequestInfo]:
	"""Return deduplicated list of PRs related to the provided commits (preserve numeric order)."""
	seen = {}
	result: List[PullRequestInfo] = []
	for c in commits:
		try:
			prs = commit_prs(owner, repo, c.sha, gh_token)
		except subprocess.CalledProcessError:
			continue
		for pr in prs:
			if pr.number not in seen:
				seen[pr.number] = True
				result.append(pr)
	return result


def format_pr_body(current_sha: str, latest_sha: str, commits: List[CommitInfo], prs: List[PullRequestInfo], owner: str, repo: str) -> str:
	"""
	Build PR body including compare link, concise commits, and a concise PR list.
	"""
	compare_url = f"https://github.com/{owner}/{repo}/compare/{current_sha}...{latest_sha}"
	lines = []
	lines.append(f"Automated engine update to commit {latest_sha}")
	lines.append("")
	lines.append(f"Compare changes: {compare_url}")
	lines.append("")
	if commits:
		lines.append("Commits included:")
		for c in commits:
			lines.append(f"- [{c.short_sha}: {c.message_title}]({c.html_url})")
	else:
		lines.append("No intermediate commits found.")

	lines.append("")

	if prs:
		lines.append("Related pull requests:")
		for p in prs:
			lines.append(f"- [#{p.number}: {p.title}]({p.html_url}) ({p.author})")
	else:
		lines.append("No related pull requests found.")
	
	return "\n".join(lines)


def try_get_remote_owner_repo() -> Tuple[str, str]:
	# Prefer GITHUB_REPOSITORY if available
	env_repo = os.environ.get("GITHUB_REPOSITORY")
	if env_repo and "/" in env_repo:
		return tuple(env_repo.split("/", 1))
	# Fallback to git remote
	try:
		out = run(["git", "remote", "get-url", "origin"], capture_output=True).stdout.strip()
	except subprocess.CalledProcessError as e:
		raise RuntimeError("Failed to get git remote URL and GITHUB_REPOSITORY not set") from e
	return parse_github_repo(out)


def download_file_from_github(owner: str, repo: str, branch: str, file_path: str, gh_token: str = "") -> Optional[str]:
	url = f"https://raw.githubusercontent.com/{owner}/{repo}/refs/heads/{branch}/{file_path}"
	cmd = ["curl", "-s", "-f"]
	if gh_token:
		# use Authorization header if token provided
		cmd += ["-H", f"Authorization: token {gh_token}"]
	cmd += [url]
	proc = subprocess.run(cmd, capture_output=True, text=True, check=False)
	if proc.returncode == 0 and proc.stdout.strip() != "":
		return proc.stdout
	return None


def local_branch_exists(branch: str) -> bool:
	try:
		run(["git", "rev-parse", "--verify", branch], capture_output=True)
		return True
	except subprocess.CalledProcessError:
		return False


def main() -> None:
	repo_root = Path(os.environ.get("GITHUB_WORKSPACE", "."))
	gh_token = os.environ.get("GITHUB_TOKEN", "")
	config_file = repo_root / "mod.config"
	if not repo_root.is_dir():
		raise FileNotFoundError("Repository root not found")
	if not config_file.is_file():
		raise FileNotFoundError("mod.config not found")

	# Change to repo root early so git commands operate in right dir
	os.chdir(repo_root)

	branch = "engine-update"
	had_checked_out_branch = False


	owner_repo = None
	try:
		owner_repo = try_get_remote_owner_repo()
	except Exception:
		# If we couldn't determine owner/repo, fall back to error
		raise RuntimeError("Failed to determine repository owner/repo")

	remote_owner, remote_repo = owner_repo

	# Read AUTOMATIC_ENGINE_SOURCE from the mod.config
	engine_source = get_var_from_file(config_file, "AUTOMATIC_ENGINE_SOURCE") or ""
	if not engine_source:
		raise ValueError("AUTOMATIC_ENGINE_SOURCE not found or empty in mod.config")
	
	current_engine_version = get_var_from_file(config_file, "ENGINE_VERSION")
	if not current_engine_version:
		raise RuntimeError("Failed to read ENGINE_VERSION from mod.config")

	print(f"Current ENGINE_VERSION in local mod.config: {current_engine_version}")

	owner, repo = parse_github_repo(engine_source)
	print(f"Engine repo: {owner}/{repo} (default branch: bleed)")

	# Retrieve latest commit in the engine repo by calling GitHub API
	default_branch = "bleed"
	commit_data = github_api_get(f"https://api.github.com/repos/{owner}/{repo}/commits/{default_branch}", gh_token)

	engine_commit_hash = None
	engine_commit_date = None
	try:
		engine_commit_hash = commit_data["sha"]
		engine_commit_date = commit_data["commit"]["author"]["date"][:10]  # YYYY-MM-DD
	except (KeyError, TypeError):
		pass

	if not engine_commit_hash or not engine_commit_date:
		print(f"Failed to get latest commit for {owner}/{repo} on branch {default_branch}", file=sys.stderr)
		print("Response was:", file=sys.stderr)
		print(json.dumps(commit_data, indent=2), file=sys.stderr)
		raise RuntimeError("Failed to obtain engine commit")

	print(f"Latest engine commit: {engine_commit_hash} ({engine_commit_date})")

	# If local mod.config already points to latest engine commit, it means the engine could have already been updated.
	# Try to delete the remote branch (which closes any open PR) and then exit.
	if current_engine_version and current_engine_version == engine_commit_hash:
		print("Local mod.config ENGINE_VERSION already matches latest engine commit.")
		try:
			proc = run(
				["gh", "pr", "list", "--head", branch, "--state", "open", "--json", "number", "--jq", ".[0].number"],
				capture_output=True,
			)
			pr_number = proc.stdout.strip()
		except subprocess.CalledProcessError:
			pr_number = ""

		if pr_number:
			print(f"Closing existing PR #{pr_number} for branch {branch}")
			try:
				run(["gh", "pr", "close", pr_number])
			except subprocess.CalledProcessError:
				print(f"Failed to close PR #{pr_number}", file=sys.stderr)

			# Also delete engine-update branch (if exists)
			try:
				run(["git", "push", "-d", "origin", "engine-update"])
			except:
				pass
		else:
			print("No open PR for engine-update branch found.")
		return

	# Check if there's an existing remote engine-update branch and check ENGINE_VERSION in its mod.config.
	downloaded = download_file_from_github(remote_owner, remote_repo, branch, "mod.config", gh_token)
	if downloaded is not None:
		current_engine_version = get_var_from_str(downloaded, "ENGINE_VERSION")
		print(f"Engine version in remote branch: {current_engine_version}")

		# If the downloaded branch already points to latest engine commit, nothing to do.
		if current_engine_version and current_engine_version == engine_commit_hash:
			print("Remote 'engine-update' mod.config already points to latest engine commit. No changes needed.")
			return

	# Update mod.config by replacing ENGINE_VERSION line
	print("Updating ENGINE_VERSION in mod.config")
	with config_file.open("r", encoding="utf-8") as f:
		lines = f.readlines()

	new_lines = []
	replaced = False
	for line in lines:
		if re.match(r"^ENGINE_VERSION\s*=", line):
			new_lines.append(f'ENGINE_VERSION="{engine_commit_hash}"')
			replaced = True
		else:
			new_lines.append(line.rstrip("\n"))
	if not replaced:
		raise RuntimeError("ENGINE_VERSION line not found in mod.config to replace")

	write_tmp_and_replace(config_file, "\n".join(new_lines))

	# Stage, commit and push
	print(f"Committing mod.config and pushing to branch {branch}")
	run(["git", "switch", "-C", branch])
	run(["git", "add", "mod.config"])
	commit_msg = f"Engine update ({engine_commit_date})"
	# Try commit; if nothing to commit, skip
	try:
		run(["git", "commit", "-m", commit_msg])
	except subprocess.CalledProcessError:
		print("No changes to commit (mod.config already up-to-date locally).")

	# Force-push
	run(["git", "push", "--force", "origin", f"{branch}:{branch}"])

	# Find open PR for this branch using gh
	print(f"Looking for existing PR for branch {branch}")
	try:
		proc = run(
			["gh", "pr", "list", "--head", branch, "--state", "open", "--json", "number", "--jq", ".[0].number"],
			capture_output=True,
		)
		pr_number = proc.stdout.strip()
	except subprocess.CalledProcessError:
		pr_number = ""

	print("Retrieving data for PR body:")

	# Retrieve new commits
	# Determine the 'current' engine version for the PR body: use previous detected current_engine_version
	print(f"... commits between {current_engine_version} and {engine_commit_hash}")
	commits = get_commits_between(owner, repo, current_engine_version, engine_commit_hash, gh_token)

	# Retrieve related PRs for those commits
	print(f"... related PRs for these commits")
	prs = aggregate_prs_for_commits(owner, repo, commits, gh_token)

	# Create new or update existing PR
	pr_title = commit_msg
	pr_body = format_pr_body(current_engine_version, engine_commit_hash, commits, prs, owner, repo)

	if pr_number:
		print(f"Updating existing PR: #{pr_number}")
		run(["gh", "pr", "edit", pr_number, "--title", pr_title, "--body", pr_body])
	else:
		print("Creating new PR")
		pr_number = run(["gh", "pr", "create", "--title", pr_title, "--body", pr_body, "--base", "master", "--head", branch], capture_output=True).stdout.strip()
		print(f"New PR: {pr_number}")


if __name__ == "__main__":
	main()
