#!/usr/bin/env python3

import os
import time
import jwt
import requests
import json

app_id = os.environ['APP_ID']
private_key = os.environ['APP_PRIVATE_KEY']
install_id = os.environ['APP_INSTALL_ID']
repo = os.environ['REPO']
owner = repo.split('/')[0]

# Create JWT
now = int(time.time())
payload = {
    "iat": now - 60,
    "exp": now + (9*60),
    "iss": app_id
}
jwt_token = jwt.encode(payload, private_key, algorithm="RS256")

headers = {
    "Authorization": f"Bearer {jwt_token}",
    "Accept": "application/vnd.github+json"
}

# Create installation access token
token_url = f"https://api.github.com/app/installations/{install_id}/access_tokens"
r = requests.post(token_url, headers=headers)
r.raise_for_status()
token = r.json().get("token")
if not token:
    print("No token in response", file=sys.stderr)
    sys.exit(4)

print(token)
