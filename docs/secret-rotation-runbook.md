# H6: Git Secret Rotation Runbook

This document outlines the **one-time manual steps** required to scrub secrets from git history and rotate all credentials. This must be done during a maintenance window.

> **WARNING:** This rewrites git history. All collaborators must re-clone after completion.

---

## Prerequisites

```bash
pip install git-filter-repo
```

---

## Step 1: Identify Sensitive Files in History

```bash
git log --all --full-history -- "**/appsettings.Development.json"
git log --all --full-history -- "**/.env"
git log --all --full-history -- "**/backup.ps1"
```

---

## Step 2: Remove Files from ALL History

```bash
# Remove appsettings.Development.json from entire history
git filter-repo --path src/LoanFlow.API/appsettings.Development.json --invert-paths --force

# If a .env file was ever committed:
git filter-repo --path .env --invert-paths --force

# Remove scripts/backup.ps1 if it contained hardcoded passwords:
# (Only if you want to purge the history of the old version)
# git filter-repo --path scripts/backup.ps1 --invert-paths --force
```

---

## Step 3: Force-Push Rewritten History

```bash
git push origin --force --all
git push origin --force --tags
```

---

## Step 4: Rotate ALL Exposed Credentials

After scrubbing history, **immediately rotate** every credential that appeared in the affected files:

| Credential | Location | Action |
|-----------|---------|--------|
| `JWT_KEY` | appsettings / env | Generate new 64-char random string → update Azure Key Vault |
| `DB_PASSWORD` | appsettings / env | Generate new strong password → update MySQL + Key Vault |
| `NapasApiKey` | appsettings | Regenerate via NAPAS vendor portal |
| `PcbApiKey` | appsettings | Regenerate via PCB portal |
| `FptEkycApiKey` | appsettings | Regenerate via FPT.AI portal |
| `Twilio:AuthToken` | appsettings | Regenerate via Twilio console |

---

## Step 5: Add Branch Protection

In GitHub → Settings → Branches → Add rule for `main`:
- ✅ Require pull request reviews before merging
- ✅ Require status checks to pass (CI Pipeline)
- ✅ **Restrict force pushes**
- ✅ **Restrict deletions**

---

## Step 6: Notify All Collaborators

All team members must re-clone the repository:

```bash
git clone <repo-url>
```

Old local copies will have diverged history and must be discarded.

---

## Verification

```bash
# Confirm the file is gone from all commits
git log --all --full-history -- "**/appsettings.Development.json"
# Should return: (empty)

# Scan for any remaining secret patterns
git grep -i "password\|secret\|apikey" $(git log --all --oneline | awk '{print $1}')
```
