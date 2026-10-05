"""Exercise the real helper lifetime with a recording transport, never a Discord account."""
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import time
import unittest

ROOT = Path(__file__).resolve().parents[1]
RUNTIME = [os.environ["MONO"]] if os.environ.get("MONO") else ([] if os.name == "nt" else ["mono"])
EXE = ROOT / "Logs/BrokerTest/VrmPresence.exe"


def wait_for(predicate, timeout=8):
    until = time.monotonic() + timeout
    while time.monotonic() < until:
        if predicate():
            return
        time.sleep(.1)
    raise AssertionError("Timed out waiting for helper")


class BrokerTests(unittest.TestCase):
    def test_real_sdk_handshake_and_graceful_activity_removal(self):
        subprocess.run(RUNTIME + [str(EXE), "--transport-test"], check=True, timeout=12)

    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="presence test ")
        self.directory = Path(self.temp.name)
        self.parent = subprocess.Popen([sys.executable, "-c", "import time; time.sleep(120)"])
        self.identity = int(subprocess.check_output(RUNTIME + [str(EXE), "--identity", str(self.parent.pid)], encoding="utf-8-sig"))
        self.children = []
        self.request = dict(Protocol=1, ParentPid=self.parent.pid, ParentStarted=self.identity,
                            Enabled=True, ApplicationId="1283700247440134174",
                            Snapshot=dict(Details="Project", State="Scene", UnityVersion="Unity 2022.3",
                                          Playing=False, Started="2026-10-05T00:00:00Z"))
        self.write()

    def write(self):
        temp = self.directory / "pending.json"
        temp.write_text(json.dumps(self.request), encoding="utf-8")
        os.replace(temp, self.directory / "request.json")

    def spawn(self):
        child = subprocess.Popen(RUNTIME + [str(EXE), str(self.directory), str(self.parent.pid), str(self.identity)])
        self.children.append(child)
        return child

    def trace(self):
        path = self.directory / "trace.jsonl"
        return [json.loads(row) for row in path.read_text().splitlines()] if path.exists() else []

    def tearDown(self):
        if self.parent.poll() is None:
            self.parent.terminate()
        self.parent.wait(timeout=5)
        for child in self.children:
            try:
                child.wait(timeout=5)
            except subprocess.TimeoutExpired:
                child.kill()
                child.wait()
        self.temp.cleanup()

    def test_reload_play_and_duplicate_launches_keep_one_connection_and_timestamp(self):
        owner = self.spawn()
        wait_for(lambda: len(self.trace()) >= 2)
        for i in range(12):
            # Recreated editor-side clients can race launches during script reload.
            duplicate = self.spawn()
            self.assertEqual(duplicate.wait(timeout=5), 0)
            self.request["Snapshot"]["Playing"] = i % 2 == 0
            self.write()
        self.request["Snapshot"]["Playing"] = True
        self.request["Snapshot"]["Started"] = "2026-10-06T00:00:00Z"
        self.write()
        wait_for(lambda: len([x for x in self.trace() if x["action"] == "send"]) == 2, timeout=20)
        trace = self.trace()
        self.assertEqual(sum(x["action"] == "connect" for x in trace), 1)
        self.assertEqual({x["pid"] for x in trace}, {owner.pid})
        self.assertEqual({x["snapshot"]["Started"] for x in trace if x["action"] == "send"}, {"2026-10-05T00:00:00Z"})
        self.assertTrue(trace[-1]["snapshot"]["Playing"])
        self.request["Enabled"] = False
        self.write()
        self.assertEqual(owner.wait(timeout=5), 0)
        self.assertEqual(self.trace()[-1]["action"], "disconnect")

    def test_editor_crash_closes_helper_and_connection(self):
        owner = self.spawn()
        wait_for(lambda: len(self.trace()) >= 2)
        self.parent.kill()
        self.parent.wait()
        self.assertEqual(owner.wait(timeout=5), 0)
        self.assertEqual(self.trace()[-1]["action"], "disconnect")

    def test_pid_reuse_does_not_connect(self):
        self.identity += 10000000
        self.request["ParentStarted"] = self.identity
        self.write()
        self.assertEqual(self.spawn().wait(timeout=5), 0)
        self.assertEqual(self.trace(), [])

    def test_helper_crash_recovers_original_timestamp(self):
        first = self.spawn()
        wait_for(lambda: len(self.trace()) >= 2)
        first.kill()
        first.wait()
        replacement = self.spawn()
        wait_for(lambda: len(self.trace()) >= 4)
        sends = [x for x in self.trace() if x["action"] == "send"]
        self.assertEqual(sends[0]["snapshot"]["Started"], sends[1]["snapshot"]["Started"])
        self.assertEqual(sends[1]["pid"], replacement.pid)

    def test_partial_request_preserves_last_connection(self):
        owner = self.spawn()
        wait_for(lambda: len(self.trace()) >= 2)
        (self.directory / "request.json").write_text("{")
        time.sleep(.5)
        self.assertIsNone(owner.poll())
        self.assertEqual(len(self.trace()), 2)
        self.write()

    def test_removing_package_stops_helper(self):
        owner = self.spawn()
        wait_for(lambda: len(self.trace()) >= 2)
        self.request["OwnershipFile"] = str(self.directory / "removed-package.cs")
        self.write()
        self.assertEqual(owner.wait(timeout=5), 0)
        self.assertEqual(self.trace()[-1]["action"], "disconnect")

    def test_application_switch_closes_old_connection_before_opening_new(self):
        self.spawn()
        wait_for(lambda: len(self.trace()) >= 2)
        self.request["ApplicationId"] = "1283700247440134175"
        self.write()
        wait_for(lambda: len(self.trace()) >= 5)
        self.assertEqual([x["action"] for x in self.trace()[:5]], ["connect", "send", "disconnect", "connect", "send"])


if __name__ == "__main__":
    unittest.main()
