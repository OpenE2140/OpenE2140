#!/usr/bin/env python3

import os
import re
import shlex
import shutil
import json
import subprocess
import sys
import tempfile
from typing import Optional, List
from dataclasses import dataclass

@dataclass
class CommitInfo:
	sha: str
	short_sha: str
	message_title: str
	html_url: str
	author_name: str
	date: str


def run(cmd: List[str], capture_output: bool = False) -> subprocess.CompletedProcess:
	return subprocess.run(cmd, capture_output=capture_output, text=True, check=True)


def get_var(file_path: str, var: str) -> Optional[str]:
	pattern = re.compile(rf'^\s*{re.escape(var)}\s*=\s*(.*)')
	with open(file_path, "r", encoding="utf-8") as f:
		for line in f:
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


def parse_github_repo(url: str) -> tuple[str, str]:
	m = re.match(r"https?://github\.com/([^/]+/[^/]+)", url)
	if not m:
		raise ValueError("Failed to parse owner/repo from engine_source")
	owner, repo = m.group(1).split("/", 1)
	return owner, repo


def write_tmp_and_replace(original: str, new_contents: str) -> None:
	fd, tmp_path = tempfile.mkstemp()
	os.close(fd)
	with open(tmp_path, "w", encoding="utf-8") as f:
		f.write(new_contents)
	shutil.move(tmp_path, original)

def github_api_get(url: str, gh_token: str = "") -> dict:
	"""Call GitHub API via curl and return parsed JSON (fail-fast on errors)."""
	cmd = ["curl", "-s", "-H", "Accept: application/vnd.github.v3+json"]
	if gh_token:
		cmd += ["-H", f"Authorization: token {gh_token}"]
	cmd += [url]

	# run and capture output
	proc = subprocess.run(cmd, capture_output=True, text=True, check=True)
	if proc.returncode != 0:
		raise RuntimeError(f"curl failed: {proc.stderr.strip()}")
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

def format_pr_body(current_sha: str, latest_sha: str, commits: List[CommitInfo], owner: str, repo: str) -> str:
	"""
	Build PR body including compare link and bullet list of commits with links and titles.
	Uses compare URL pattern specified by user.
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
	return "\n".join(lines)


def main() -> None:
	repo_root = os.environ.get("GITHUB_WORKSPACE", ".")
	gh_token = os.environ.get("GITHUB_TOKEN", "")
	config_file = os.path.join(repo_root, "mod.config")
	if not os.path.isfile(config_file):
		raise FileNotFoundError("mod.config not found")

	current_engine_version = get_var(config_file, "ENGINE_VERSION") or ""
	engine_source = get_var(config_file, "AUTOMATIC_ENGINE_SOURCE") or ""

	if not current_engine_version:
		raise ValueError("ENGINE_VERSION not found or empty in mod.config")
	if not engine_source:
		raise ValueError("engine_source not found or empty in mod.config")

	print(f"Current ENGINE_VERSION: {current_engine_version}")
	print(f"AUTOMATIC_ENGINE_SOURCE: {engine_source}")

	owner, repo = parse_github_repo(engine_source)
	print(f"Engine repo: {owner}/{repo} (default branch: bleed)")

	# Retrieve latest commit in the engine repo by calling GitHub API
	default_branch = "bleed"

	commit_data = github_api_get(f"https://api.github.com/repos/{owner}/{repo}/commits/{default_branch}", gh_token)

	# Extract latest engine commit details
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

	if current_engine_version == engine_commit_hash:
		print("ENGINE_VERSION already up-to-date. Exiting.")
		return

	# Update mod.config by replacing ENGINE_VERSION line
	with open(config_file, "r", encoding="utf-8") as f:
		lines = f.readlines()

	new_lines = []
	replaced = False
	for line in lines:
		if re.match(r"^ENGINE_VERSION\s*=", line):
			new_lines.append(f'ENGINE_VERSION="{engine_commit_hash}"\n')
			replaced = True
		else:
			new_lines.append(line)
	if not replaced:
		raise RuntimeError("ENGINE_VERSION line not found in mod.config to replace")

	write_tmp_and_replace(config_file, "".join(new_lines))

	os.chdir(repo_root)

	# Create or reset branch locally
	branch = "engine-update"
	run(["git", "checkout", "-B", branch])

	# Stage and commit (git commit will fail if no changes; let it raise)
	run(["git", "add", "mod.config"])
	commit_msg = f"Engine update ({engine_commit_date})"
	run(["git", "commit", "-m", commit_msg])

	# Force-push
	run(["git", "push", "--force", "origin", branch])

	# Find open PR for this branch using gh
	try:
		proc = run(
			["gh", "pr", "list", "--head", branch, "--state", "open", "--json", "number", "--jq", ".[0].number"],
			capture_output=True,
		)
		pr_number = proc.stdout.strip()
	except subprocess.CalledProcessError:
		pr_number = ""
	
	# Retrieve new commits
	commits = get_commits_between(owner, repo, current_engine_version, engine_commit_hash, gh_token)

	# Create new or update existing PR
	pr_title = commit_msg
	pr_body = format_pr_body(current_engine_version, engine_commit_hash, commits, owner, repo)

	if pr_number:
		print(f"Open PR exists: #{pr_number}")
		run(["gh", "pr", "edit", pr_number, "--title", pr_title, "--body", pr_body])
	else:
		print("Creating new PR")
		pr_number = run(["gh", "pr", "create", "--title", pr_title, "--body", pr_body, "--base", "master", "--head", branch], capture_output=True).stdout.strip()
		print(f"New PR: {pr_number}")


if __name__ == "__main__":
	main()
