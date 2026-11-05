# Hardware acceleration results

Results of T6.7 (manual hardware checklist). Fill in per vendor as hardware is tested.

| Vendor | OS | GPU | Driver | FFmpeg | `drawplate -l` | Result |
|--------|----|-----|--------|--------|----------------|--------|
| NVIDIA | | | | | | |
| Intel | | | | | | |
| AMD | | | | | | |
| Apple | | | | | | |

Mark each section [V] when verified on real hardware. Drawplate never installs GPU drivers; they are the user's job.

## Hardware note

When a GPU is detected but no hardware encoder passes, Drawplate prints a neutral informational note (N-GPU-01 / N-GPU-02). It never changes the exit code and is never a warning.
