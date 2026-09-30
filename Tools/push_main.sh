#!/usr/bin/env bash
# Publishes the current work straight to main (fast-forward only, never force) and keeps the work branch in sync.
# The user asked for direct pushes to main so nothing has to be merged by hand.
set -euo pipefail
BRANCH=claude/project-analysis-tools-j29o1u
git fetch -q origin main
if ! git merge-base --is-ancestor origin/main HEAD; then
  echo "main has new commits: merging them first"
  git merge --no-edit origin/main
fi
dotnet test Tools/SimTests/SimTests.csproj --nologo 2>&1 | grep -E "Passed!|Failed!" 
python3 Tools/CompileCheck/check.py | tail -1
git push origin HEAD:main
git push -q origin HEAD:"$BRANCH"
