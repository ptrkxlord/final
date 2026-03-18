"""
core/bridge_cli.py — CLI for bridge management
Run: python -m core.bridge_cli
"""

import sys
import json
import argparse
from core.bridge_manager import bridge_manager
from core.obfuscation import decrypt_string

def main():
    parser = argparse.ArgumentParser(description='Bridge Manager CLI')
    parser.add_argument('--list', action='store_true', help='List all bridges')
    parser.add_argument('--deploy', action='store_true', help='Deploy new bridge')
    parser.add_argument('--test', metavar='URL', help='Test a bridge URL')
    parser.add_argument('--remove', metavar='URL', help='Remove a bridge')
    parser.add_argument('--stats', action='store_true', help='Show stats')
    parser.add_argument('--switch', action='store_true', help='Switch to next bridge')

    args = parser.parse_args()

    if args.list:
        print(decrypt_string("MlxFVnNxGzATMy19IRlbR1M="))
        for i, bridge in enumerate(bridge_manager.get_all_bridges()):
            current = " [ACTIVE]" if i == bridge_manager.current_index else ""
            print(decrypt_string("FVsFUW4qOxAzEw1dD0IFDxxAHQU6LA=="))

    elif args.deploy:
        print(decrypt_string("KlcIByEoMAw9VwRdBRkECAdWHw5gf3c="))
        url = bridge_manager.force_deploy()
        if url:
            print(decrypt_string("jK79Swo0KQ41Dg9cSBkdDxxeBQ=="))
        else:
            print("❌ Failed to deploy")

    elif args.test:
        print(decrypt_string("OlcLHyc/PkIhFhhfARcSHx1GBUVgfw=="))
        if bridge_manager.test_bridge(args.test):
            print("✅ Bridge is working")
        else:
            print("❌ Bridge is dead")

    elif args.remove:
        print(decrypt_string("PFcVBDg4NwV6DAtKFUpICAtfFx0rLHdMdA=="))
        if bridge_manager.remove_dead_bridge(args.remove):
            print("✅ Removed")
        else:
            print("❌ Not found")

    elif args.stats:
        stats = bridge_manager.get_stats()
        print(decrypt_string("MlxFVnNxGzATMy19UmoyOzphWFZzbA=="))
        print(json.dumps(stats, indent=2, default=str))

    elif args.switch:
        if bridge_manager._switch_to_next():
            print(decrypt_string("jK79Sx0mMBY5Hw9cUk0JWhVQCgIqNjw9NxYEWRVcFFQJVww0LSQrED8ZHmcQSw8eCVdQQjM="))
        else:
            print("❌ Failed to switch")

    else:
        parser.print_help()

if __name__ == "__main__":
    main()
