"""Build the VRChat listing template's source.json contract with no third-party runtime.

Historical manifests are retained explicitly; rebuilding an unchanged listing cannot
erase versions. Authentication is sent only to the GitHub API, never download hosts.
"""
import argparse
import copy
import hashlib
import io
import json
import os
from pathlib import Path
import re
import shutil
import urllib.parse
import urllib.request
import zipfile

ROOT = Path(__file__).resolve().parents[1]
MAX_DOWNLOAD = 100 * 1024 * 1024


class PublicRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        redirected = super().redirect_request(req, fp, code, msg, headers, newurl)
        if urllib.parse.urlsplit(newurl).scheme != "https":
            raise ValueError("Refusing a non-HTTPS redirect")
        if redirected:
            redirected.remove_header("Authorization")
        return redirected


def fetch(url):
    parsed = urllib.parse.urlsplit(url)
    if parsed.scheme != "https" or parsed.username or parsed.password:
        raise ValueError("Sources must use HTTPS without embedded credentials")
    headers = {"User-Agent": "Varkaria-VPM-Listing"}
    if parsed.hostname == "api.github.com" and os.environ.get("GH_TOKEN"):
        headers["Authorization"] = "Bearer " + os.environ["GH_TOKEN"]
    request = urllib.request.Request(url, headers=headers)
    with urllib.request.build_opener(PublicRedirect()).open(request, timeout=60) as response:
        data = response.read(MAX_DOWNLOAD + 1)
        if len(data) > MAX_DOWNLOAD:
            raise ValueError("Download exceeds 100 MiB")
        return data


def release_urls(repository, download=fetch):
    if not re.fullmatch(r"[\w.-]+/[\w.-]+", repository):
        raise ValueError("Invalid GitHub repository")
    for page in range(1, 1001):
        releases = json.loads(download(f"https://api.github.com/repos/{repository}/releases?per_page=100&page={page}"))
        for release in releases:
            if release["draft"] or release["prerelease"]:
                continue
            for asset in release["assets"]:
                if asset["name"].endswith(".zip"):
                    yield asset["browser_download_url"]
        if len(releases) < 100:
            return
    raise ValueError("Release pagination exceeded its safety limit")


def read_package(data, url):
    with zipfile.ZipFile(io.BytesIO(data)) as archive:
        if archive.namelist().count("package.json") != 1:
            raise ValueError(f"{url}: expected one package.json at ZIP root")
        info = archive.getinfo("package.json")
        if info.file_size > 1024 * 1024:
            raise ValueError("Oversized package manifest")
        package = json.loads(archive.read(info))
    if not re.fullmatch(r"[a-z0-9][a-z0-9._-]+", package["name"]):
        raise ValueError("Invalid package ID")
    if not re.fullmatch(r"\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?(?:\+[0-9A-Za-z.-]+)?", package["version"]):
        raise ValueError("Invalid package version")
    package["url"] = url
    package["zipSHA256"] = hashlib.sha256(data).hexdigest()
    return package


def build(source, history, download=fetch, offline=False):
    if source["id"] != history["id"]:
        raise ValueError("Changing the repository ID would break existing subscribers")
    result = {key: copy.deepcopy(source[key]) for key in ("name", "id", "url", "author", "description")}
    result["packages"] = copy.deepcopy(history["packages"])
    urls = {}
    if not offline:
        for repository in source.get("githubRepos", []):
            urls.update((url, None) for url in release_urls(repository, download))
        for package in source.get("packages", []):
            urls.update((url, package["name"]) for url in package["releases"])
    for url, expected in sorted(urls.items()):
        package = read_package(download(url), url)
        if expected and package["name"] != expected:
            raise ValueError("Explicit source package ID does not match archive")
        versions = result["packages"].setdefault(package["name"], {"versions": {}})["versions"]
        previous = versions.get(package["version"])
        if previous:
            if previous["zipSHA256"] != package["zipSHA256"]:
                raise ValueError(f"Published bytes changed for {package['name']} {package['version']}")
            # Preserve historical metadata and URLs byte-for-byte as data.
            continue
        versions[package["version"]] = package
    return result


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", type=Path, default=ROOT / ".site")
    parser.add_argument("--offline", action="store_true", help="Preview historical entries without fetching releases")
    args = parser.parse_args()
    source = json.loads((ROOT / "source.json").read_text())
    history = json.loads((ROOT / "listing/history.json").read_text())
    result = build(source, history, offline=args.offline)
    shutil.copytree(ROOT / "Website", args.output, dirs_exist_ok=True)
    (args.output / "index.json").write_text(json.dumps(result, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    print(f"Built {len(result['packages'])} packages, {sum(len(p['versions']) for p in result['packages'].values())} versions")


if __name__ == "__main__":
    main()
