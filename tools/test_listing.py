import copy
import hashlib
import io
import json
from pathlib import Path
import tempfile
import unittest
import urllib.request
import zipfile
import listing
import package

ROOT = Path(__file__).resolve().parents[1]


def archive(version="0.1.0", name="dev.varkaria.discordrpc", manifest_path="package.json"):
    stream = io.BytesIO()
    with zipfile.ZipFile(stream, "w") as target:
        target.writestr(manifest_path, json.dumps(dict(name=name, version=version, unity="2022.3")))
    return stream.getvalue()


class ListingTests(unittest.TestCase):
    def setUp(self):
        self.source = json.loads((ROOT / "source.json").read_text())
        self.history = json.loads((ROOT / "listing/history.json").read_text())
        self.source["githubRepos"] = []

    def test_unchanged_rebuild_retains_every_historical_version(self):
        result = listing.build(self.source, self.history)
        self.assertEqual(result["packages"], self.history["packages"])
        self.assertEqual(len(result["packages"]["dev.varkaria.discordrpc"]["versions"]), 5)
        self.assertEqual(listing.build(self.source, result), result)
        self.assertEqual(result["url"], "https://vpm.varkaria.works/index.json")

    def test_new_package_and_version_keep_history_and_real_checksum(self):
        self.source["packages"] = [dict(name="dev.varkaria.discordrpc", releases=["https://example.com/package.zip"])]
        data = archive()
        result = listing.build(self.source, self.history, lambda _: data)
        versions = result["packages"]["dev.varkaria.discordrpc"]["versions"]
        self.assertEqual(len(versions), 6)
        self.assertEqual(versions["0.1.0"]["zipSHA256"], hashlib.sha256(data).hexdigest())
        for version, manifest in self.history["packages"]["dev.varkaria.discordrpc"]["versions"].items():
            self.assertEqual(versions[version], manifest)

    def test_published_version_cannot_silently_change_bytes(self):
        self.source["packages"] = [dict(name="dev.varkaria.discordrpc", releases=["https://example.com/package.zip"])]
        with self.assertRaisesRegex(ValueError, "Published bytes changed"):
            listing.build(self.source, self.history, lambda _: archive("0.0.5"))

    def test_wrong_package_rejected(self):
        self.source["packages"] = [dict(name="expected", releases=["https://example.com/package.zip"])]
        with self.assertRaisesRegex(ValueError, "does not match"):
            listing.build(self.source, self.history, lambda _: archive())

    def test_wrong_archive_layout_rejected(self):
        with self.assertRaisesRegex(ValueError, "ZIP root"):
            listing.read_package(archive(manifest_path="folder/package.json"), "url")

    def test_repository_id_is_stable(self):
        self.source["id"] = "new.id"
        with self.assertRaisesRegex(ValueError, "repository ID"):
            listing.build(self.source, self.history)

    def test_drafts_prereleases_and_non_zip_assets_are_excluded(self):
        releases = [dict(draft=False, prerelease=False, assets=[dict(name="package.zip", browser_download_url="stable"), dict(name="other.txt", browser_download_url="text")]),
                    dict(draft=True, prerelease=False, assets=[dict(name="package.zip", browser_download_url="draft")]),
                    dict(draft=False, prerelease=True, assets=[dict(name="package.zip", browser_download_url="preview")])]
        self.assertEqual(list(listing.release_urls("owner/repo", lambda _: json.dumps(releases))), ["stable"])

    def test_download_failure_fails_build_instead_of_erasing_versions(self):
        self.source["githubRepos"] = ["owner/repo"]
        def fail(_): raise OSError("Unavailable")
        with self.assertRaises(OSError): listing.build(self.source, self.history, fail)

    def test_redirect_does_not_forward_github_authorization(self):
        request = urllib.request.Request("https://api.github.com/test", headers={"Authorization": "Bearer fake"})
        redirect = listing.PublicRedirect().redirect_request(request, None, 302, "Found", {}, "https://other.example/test")
        self.assertIsNone(redirect.get_header("Authorization"))

    def test_vixen_is_pending_and_not_in_live_source(self):
        source = json.loads((ROOT / "source.json").read_text())
        self.assertNotIn("varkaria/vixen-plus", source["githubRepos"])
        pending = json.loads((ROOT / "listing/vixen-plus.pending.json").read_text())
        self.assertEqual(len(pending["packages"]), 3)
        for item in pending["packages"]:
            data = archive("1.5.0", item["name"])
            result = listing.build({**self.source, "packages": [item]}, self.history, lambda _: data)
            self.assertIn("1.5.0", result["packages"][item["name"]]["versions"])

    def test_package_reproducibility_guid_and_ignored_helper_preservation(self):
        with tempfile.TemporaryDirectory() as directory:
            base = Path(directory)
            source = base / "package"
            source.mkdir()
            (source / "package.json").write_text('{"name":"test.package","version":"1.0.0"}')
            (source / "package.json.meta").write_text("guid: unchanged")
            helper = source / "Editor/Broker~/helper.exe"
            helper.parent.mkdir(parents=True)
            helper.write_bytes(b"test helper")
            first = package.package(source, base / "one")
            second = package.package(source, base / "two")
            self.assertEqual(first.read_bytes(), second.read_bytes())
            with zipfile.ZipFile(first) as zipped:
                self.assertEqual(zipped.read("package.json.meta"), b"guid: unchanged")
                self.assertEqual(zipped.read("Editor/Broker~/helper.exe"), b"test helper")


if __name__ == "__main__": unittest.main()
