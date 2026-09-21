# SignPath checklist

Getting Hangly for Windows signed, for free, by **SignPath Foundation**.

Two decisions are settled and are not re-opened here: **no paid certificate, and no Azure
Trusted Signing, ever.** SignPath Foundation is the route. The reasoning, and what a user
sees before and after signing, is in `Docs/DISTRIBUTION.md` §3.

**This is the longest lead time in the project and the least technical thing in it.** It
is measured in weeks of somebody else's calendar, and none of that time starts until the
enquiry is sent.

---

## 1. Their conditions, and where Hangly stands

| Condition | Hangly | Evidence |
|---|---|---|
| OSI-approved licence, no commercial dual-licensing | MIT, as `LICENSE` | **[DOC]** in the repo |
| Public repository | `github.com/SharanCreatedThis/Hangly-Windows` | **[DOC]** |
| **Already released in the form to be signed** | **Not yet** — see §2 | — |
| Actively maintained | Yes | **[DOC]** commit history |
| Functionality described on a download page | The product page exists; it must describe what the app does | **[WIN]** check before applying |
| No malware or security-circumvention tooling | An ornament that hangs a charm on a desktop | **[DOC]** |
| Verifiable automated build from that repository | GitHub Actions builds and packages; artifacts come from the run | **[DOC]** `release.yml` |
| Every release manually approved before signing | Not yet configured | §4 |

## 2. The ordering problem

SignPath will not sign a project that **has not released yet**, and the first release is
therefore unsigned by construction. There is no way around this and nothing is being done
wrong.

- [ ] Publish **v0.9.0**, unsigned. `SHIP-CHECKLIST.md` covers it.
- [ ] Ask them directly whether a GitHub **pre-release** satisfies "already released".
      Their terms and their GitHub integration docs are both silent on it, so this is a
      question, not an assumption in either direction.

## 3. Send the enquiry

The draft has been written and **has still not been sent**. It has been outstanding for
weeks, and everything downstream waits on it.

- [ ] Read the draft at `~/Documents/hangly-shots/signpath-enquiry.md`.
- [ ] Send it, via https://signpath.org/apply (or the address the draft names).
- [ ] Record the date sent, here: `sent: ____________`
- [ ] Chase after two weeks with no reply.

## 4. Once they say yes

- [ ] Install the **SignPath GitHub App** on the repository. Signing must happen inside
      the workflow run that produced the artifact — an artifact uploaded by a token holder
      does not satisfy their verifiable-build condition.
- [ ] Create the SignPath project, artifact configuration and signing policy.
- [ ] Add `SIGNPATH_API_TOKEN` (and the organisation, project and policy identifiers) as
      repository secrets.
- [ ] Set the approver, so every release needs a human before it is signed. This is their
      requirement, not an option.

## 5. Wire it into the release

`vpk pack` already has the seam. `--signTemplate` takes a command with `{{file}}`
substituted, and without it the pack says exactly what it skipped:

```
[WRN] No signing parameters provided, 289 file(s) will not be signed.
```

So signing drops into the existing pack step rather than restructuring the workflow.

- [ ] Add `--signTemplate` to the `Package` step in `.github/workflows/release.yml`.
- [ ] Add a gate that **fails the release** when signing was expected and skipped. The
      warning above is a line in a log nobody reads; an unsigned release that was meant to
      be signed must not be publishable by accident.
- [ ] Remove the unsigned warning that the publish job appends to the release notes, and
      remove it from the download page.

## 6. Verify a signed build

- [ ] `Get-AuthenticodeSignature` on `Hangly.exe`, `Update.exe` and the `Setup.exe`
      reports **Valid**, with the expected publisher name.
- [ ] Install the signed build on a machine that has never seen Hangly. SmartScreen will
      still warn — a signed download from a publisher with no history shows *"Windows
      protected your PC"* **with the verified publisher name**. That is the expected
      result, not a failure.

## 7. Reputation

Reputation accrues against a **consistent signing identity**, across versions, as
downloads accumulate. Microsoft publishes no threshold and describes it as weeks.

- [ ] Sign every release from here on, with the same identity. A version that ships
      unsigned does not merely miss out — it starts the count again.
- [ ] Say so plainly on the download page rather than promising a clean install on day
      one.
