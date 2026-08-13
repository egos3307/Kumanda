# CloudPad Protocol v1

CloudPad uses newline-delimited UTF-8 JSON over TCP for pairing/heartbeat and a fixed 61-byte little-endian UDP packet for real-time input. TCP and UDP share the configured port (default `26760`). No discovery or public relay exists.

## Pairing

Client sends one line: `{"type":"HELLO","protocolVersion":1,"deviceName":"Pixel","pin":"123456"}`. Receiver replies with either `PAIR_ACCEPTED` containing an unsigned 32-bit `sessionId` and Base64 32-byte `sessionToken`, or `ERROR`. A restart clears the in-memory session. PIN/token are never logged.

Heartbeat lines are `{"type":"PING","timestamp":123}` and matching `PONG`. The timestamp is monotonic on Android and is used only by that same device to calculate round-trip time.

## UDP input packet

| Offset | Size | Field |
|---:|---:|---|
| 0 | 1 | protocol version (`1`) |
| 1 | 4 | session id |
| 5 | 4 | sequence number |
| 9 | 8 | Unix timestamp, milliseconds |
| 17 | 2 each | LX, LY, RX, RY signed axes |
| 25 | 1 each | LT, RT unsigned triggers |
| 27 | 2 | button bitmask |
| 29 | 32 | session token |

Axes map `-1..1` to `-32768..32767`; triggers map `0..1` to `0..255`. Button bits 0–13 are A, B, X, Y, LB, RB, Back, Start, L3, R3, D-pad Up, Down, Left, Right. Wrap-aware sequence comparison rejects stale packets. After the configured timeout (default 500 ms), receiver submits a neutral state.
