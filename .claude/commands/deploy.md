---
description: Kill running WhisperHeim, rebuild, and launch.
allowed-tools: Bash
---

Run the deterministic deploy script:

!`./deploy.cmd`

If the script reports a non-zero exit, surface the error to the user and do not retry. Otherwise, confirm the new exe was launched.
