# Static Update Manifest Design

## Outcome

Version 1.1.13 checks a static update manifest before querying the GitHub Releases API. The manifest carries the current release version, fixed GitHub installer URL, exact byte size, SHA-256, and release page URL. It is published from the repository's `master` branch through `raw.githubusercontent.com`, so discovering an update does not consume the API quota shared by users on one public IP address.

## Compatibility Boundary

This change cannot alter the behavior of installed 1.1.11 or 1.1.12 clients; they do not contain the manifest reader. Those clients can install the already released 1.1.12 manually or retry after the GitHub API allowance resets. The static manifest reader begins with the next application release.

## Security

- The manifest URL is a fixed HTTPS `raw.githubusercontent.com` URL for this repository and branch; redirected or non-HTTPS locations are rejected.
- The manifest is parsed strictly: version must be newer, the installer name must be `HuahaiClipboard-Setup.exe`, the installer URL must remain on `github.com`, size must be positive, and SHA-256 must be exactly 64 hex characters.
- Downloaded bytes continue to be checked against size and SHA-256.
- Before installation, the existing pinned publisher certificate verification remains mandatory. Therefore a modified public manifest cannot cause the app to start an unsigned or differently signed installer.
- A missing, stale, malformed, or unavailable manifest is not an update failure. The existing API and Release-page fallback remains available.

## Release Contract

`update-manifest.json` is a tracked root-level release artifact. A release maintainer updates it with the exact signed installer metadata before publishing the corresponding tag. It contains no credentials, user data, or mutable download host.

## Verification

- A valid manifest yields an auto-installable update without any API request.
- Invalid host, malformed SHA-256, wrong asset name, and non-positive size are rejected before a download begins.
- A manifest with the current or older version does not suppress the API fallback.
- Existing API ETag, rate-limit, Release-page, download-size, SHA-256, and publisher-signature tests remain green.
