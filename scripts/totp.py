#!/usr/bin/env python3
"""Prints the current 6-digit TOTP code for a base32 secret (RFC 6238, SHA-1, 30 s, 6 digits), as an authenticator app would.

    python3 scripts/totp.py JBSWY3DPEHPK3PXPJBSWY3DPEHPK3PXP

Development helper for signing in to the web app with the MFA secrets that `make simulate` prints. Standard library only.
"""
import base64
import hashlib
import hmac
import struct
import sys
import time


def totp(secret_b32: str, at: float | None = None, step: int = 30, digits: int = 6) -> str:
    secret = secret_b32.strip().replace(" ", "").upper()
    key = base64.b32decode(secret + "=" * (-len(secret) % 8))
    counter = struct.pack(">Q", int((at if at is not None else time.time()) // step))
    mac = hmac.new(key, counter, hashlib.sha1).digest()
    offset = mac[-1] & 0x0F
    code = (struct.unpack(">I", mac[offset:offset + 4])[0] & 0x7FFFFFFF) % (10 ** digits)
    return str(code).zfill(digits)


if __name__ == "__main__":
    if len(sys.argv) != 2 or sys.argv[1] in ("-h", "--help"):
        print(__doc__.strip())
        sys.exit(0 if len(sys.argv) == 2 else 2)
    remaining = 30 - int(time.time()) % 30
    print(f"{totp(sys.argv[1])}  (valid for {remaining}s)")
