#!/usr/bin/env sh
# setup.sh
#
# One-time setup for this repo. Run this once after cloning:
#
#   ./setup.sh
#
# What it does:
#   - Points git at the tracked .githooks/ folder instead of the default
#     (untracked, per-clone) .git/hooks/ folder, so hooks committed to this
#     repo actually run on your machine.

set -e

REPO_ROOT="$(git rev-parse --show-toplevel 2>/dev/null)"
if [ -z "$REPO_ROOT" ]; then
  echo "❌ Not inside a git repository. Run this from within the cloned repo."
  exit 1
fi

cd "$REPO_ROOT"

git config core.hooksPath .githooks
chmod +x .githooks/*

echo "✅ Git hooks configured (core.hooksPath -> .githooks/)"
echo "   apps/portfolio-alpine/writing/ is now protected from manual commits."