#!/bin/bash
cd "$(dirname "$0")" || exit 1
node mac/cli.mjs "$@"
