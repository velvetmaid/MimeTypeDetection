# MimeTypeDetection for OutSystems ODC

A lightweight .NET External Library for identifying uploaded **Binary Data** and
applying upload policies in OutSystems Developer Cloud. This is a class library,
not a standalone application. It performs no filesystem or network access while
inspecting files; `FileName` is metadata, **never a server-side path**.

**MIME identification and policy validation are not malware scanning, complete
format validation, or proof that content is safe.** A recognizable signature or
Office package can be forged. Apply application authorization, storage limits,
safe download headers, and any required malware/content scanning separately.

## Public ODC actions

All actions belong to the existing `MimeTypeDetection` interface. Pass the
**complete file**, not just a header, as `FileContent` (Binary Data).

| Action | Inputs | Output |
| --- | --- | --- |
| `GetFileMimeType` (legacy) | `FileName` (Text), `FileContent` (Binary Data) | Existing `FileTypeRecord`: `Extension`, `MimeType`, `ErrorMessage` |
| `DetectMimeType` | `FileName` (Text), `FileContent` (Binary Data) | `DetectionResult` |
| `ValidateFile` | `FileName` (Text), `FileContent` (Binary Data), `Options` (`ValidationOptions`) | `ValidationResult` |

`DetectionResult` contains:

| Field | Meaning |
| --- | --- |
| `MimeType` | Identified MIME type; normally `application/octet-stream` when unknown. Ambiguous OLE uses `application/x-ole-storage`; ambiguous Office ZIP uses `application/zip`. |
| `DetectedExtension` | Canonical lowercase extension including the dot, or empty if no supported specific type is identified. |
| `DetectionMethod` | `Signature`, `Container`, or `Unknown`. Never filename inference. |
| `IsRecognized` | A supported type was identified. Does not imply integrity or safety. |
| `ExtensionMismatch` | For recognized types, the supplied extension disagrees or is missing. Always false when no specific type was identified. |
| `ErrorCode`, `ErrorMessage` | Empty on successful identification; stable code and safe diagnostic otherwise. |

Common aliases `.jpeg`/`.jpe` for JPEG and `.tiff` for TIFF are accepted. PE
executables and DLLs share a MIME type; `.dll` is accepted with canonical `.exe`.
Both slash styles are handled consistently when comparing filename extensions.

### Detection versus validation

Detection identifies binary evidence, regardless of filename. A mismatch is
reported, not a detection failure. **There is no extension fallback**, including
for text files or empty content. The large historical MIME mapping is not a list
of supported binary formats.

Validation always requires a recognized type and applies these options:

| `ValidationOptions` field | ODC type | Behavior |
| --- | --- | --- |
| `MaxFileSizeBytes` | Long Integer | `0` uses the hard 50 MiB limit. Positive values must be at most `52428800`; the boundary is inclusive. Negative/larger values are invalid options. |
| `AllowedMimeTypes` | Text | Comma- or semicolon-separated exact MIME types, case insensitive, whitespace trimmed. Empty permits any recognized type. No wildcards, MIME parameters, or filename-extension lists. Maximum 4096 characters. |
| `RequireMatchingExtension` | Boolean | If true, reject missing or mismatched extensions. Default false. |

`ValidationResult` contains `Detection` (the full `DetectionResult`), `IsValid`,
`ErrorCode`, and `ErrorMessage`. The outer error represents the first detection
or policy failure. A policy failure does not overwrite successful detection
evidence. Invalid options return `Detection.ErrorCode = "NOT_INSPECTED"`.
Checks run in this order: options, size, binary identification, allowlist,
extension policy. A default options structure permits **any recognized format**,
including executables and archives; use a narrow allowlist for uploads.

### Practical ODC examples

After importing the library, add its actions to your app's dependencies. In an
upload-handling **server action**, bind the upload's filename to `FileName` and
its Binary Data to `FileContent` (do not pass a local path or Base64 text).

**Identify and display a type**

1. Call `DetectMimeType` with the uploaded bytes and original name.
2. If `IsRecognized = False`, use `ErrorCode` to choose an application message.
3. Otherwise use `MimeType` and `DetectedExtension`. Decide separately whether
   `ExtensionMismatch` should block the upload.

Example: a JPEG named `invoice.exe` yields `image/jpeg`, `.jpg`,
`IsRecognized = True`, `ExtensionMismatch = True`.

**Accept PDF and JPEG uploads up to 5 MiB**

Assign a `ValidationOptions` variable:

```text
MaxFileSizeBytes = 5242880
AllowedMimeTypes = "application/pdf,image/jpeg"
RequireMatchingExtension = True
```

Call `ValidateFile`. Continue storage/processing only if `IsValid = True`.
For `EXTENSION_MISMATCH`, ask the user to correct the filename; do not change
the detection result to match it. For `MIME_NOT_ALLOWED`, reject the file even
if its filename ends in `.pdf`. A Word document named `upload.zip` is still
identified as DOCX when its in-memory package metadata supports that result.

## Supported formats and limitations

| Category | Identification |
| --- | --- |
| Images | PNG, JPEG, GIF87a/89a, BMP, ICO, little-/big-endian TIFF; WebP by RIFF identifier |
| Documents | PDF header starting within the first 1024 bytes; DOCX/XLSX/PPTX via ZIP metadata, content-type declarations, package-level Office relationships, and a nonempty main part |
| Media | RIFF AVI/WAV; MP4 with supported major brands (`isom`, `iso2`, `mp41`, `mp42`, `avc1`, `M4V `); MP3 Layer III with two complete consistent frames, optionally preceded by ID3v2 |
| Generic media containers | Ogg (`application/ogg`) and ASF (`application/vnd.ms-asf`); codecs/audio-versus-video are not guessed |
| Archives/other | ZIP, RAR4/5, 7z, TAR at offset 257, gzip, bzip2, Unix compress, XAR, CAB, ARJ, LZH, RPM, CHM, SWF, TTF, PE executables/DLLs, ISO9660 primary descriptor at offset 32768, DMG/VHD footer signatures |

- OLE identifies a shared container, **not** DOC/XLS/PPT/MSG. It returns
  `AMBIGUOUS_FORMAT`, empty extension, and `IsRecognized = False`. Encrypted
  Office files stored in OLE are also unsupported. No filename-based subtype
  inference or fixed-sector guessing is performed.
- EBML alone cannot distinguish Matroska from WebM; it returns
  `AMBIGUOUS_FORMAT`. Unknown RIFF subtypes are likewise ambiguous.
- A generic ZIP remains ZIP, even if named `.docx` or containing a `word/`
  directory. Missing Office relationships/main parts, unsupported Office types
  (including macro-enabled/template formats), or conventional `vbaProject.bin`
  payloads do not produce macro-free DOCX/XLSX/PPTX results. This is not macro
  scanning; a malicious package can disguise its contents.
- Office relationships use direct package paths. Unusual URI-escaped or
  dot-segment targets are not normalized and may remain generic ZIP.
- ZIP inspection checks structure and selected XML metadata, **not all entry
  contents, CRCs, or complete Office schemas**. Corrupt metadata is rejected,
  but corruption in uninspected payloads may not be detected. Signature-based
  formats are also not fully parsed: a recognizable header can match an
  otherwise truncated/invalid file.
- Text, JSON, XML, HTML, SVG, CSV, torrents, HEIC, QuickTime, unsupported MP4
  brands, and other unrecognized data return generic binary, not an extension
  guess. Some formerly accepted weak/shared signatures have deliberately been
  removed or made stricter.

### Resource limits

- Complete binary input: **50 MiB** maximum. Enforce smaller application/upload
  limits before calling the library where possible: the Binary Data argument
  has already been allocated/marshaled when these checks run.
- ZIP: at most **2048 entries**, **1 MiB central directory**, and **200 MiB total
  declared uncompressed size**. Directory metadata is preflighted before
  `ZipArchive` creates entry objects, including checking declared entry counts.
- Selected XML metadata: at most **64 KiB each** for `[Content_Types].xml` and
  `_rels/.rels`, with bounded reads even if declared sizes are false. No other
  payload is decompressed; archives are never recursively inspected/extracted.
- XML DTDs and external resolution are disabled. No network requests are made.
- ZIP64, multi-volume, encrypted ZIPs, and ZIP compression methods other than
  stored/deflate are not supported. Self-extracting/prefixed ZIPs are not
  identified as ZIP. Valid archives outside these limits are rejected, not
  treated as proven Office documents.

### Error handling

Branch on codes, not message text. Public diagnostics never include raw
exception messages or filesystem paths.

| Code | Meaning |
| --- | --- |
| `EMPTY_CONTENT` | Null/zero-length bytes |
| `FILE_TOO_LARGE` | Hard or configured byte limit exceeded |
| `UNSUPPORTED_FORMAT` | Insufficient evidence or unsupported binary format |
| `TRUNCATED_CONTENT` | An identified RIFF header is incomplete |
| `AMBIGUOUS_FORMAT` | A shared container cannot identify one supported subtype |
| `INVALID_ARCHIVE` | Invalid ZIP/XML metadata or unsupported ZIP feature |
| `ARCHIVE_LIMIT_EXCEEDED` | ZIP inspection resource limit exceeded |
| `INVALID_OPTIONS` | Invalid validation size or MIME allowlist |
| `MIME_NOT_ALLOWED` | Recognized MIME type rejected by policy |
| `EXTENSION_MISMATCH` | Extension policy rejected the filename |
| `NOT_INSPECTED` | Detection skipped because validation options were invalid |

Expected malformed/unsupported inputs return structured results. Process-level
failures such as exhausted memory are not swallowed or converted to success.

## Compatibility and migration

The legacy `GetFileMimeType` action and three-field `FileTypeRecord` contract are
unchanged. New actions and structures are additive. However, **behavior is
intentionally stricter**: extension-only identification is removed, Office
inspection uses the supplied bytes, ambiguous containers are not mislabeled,
and MIME mappings such as RPM, TTF, CAB, CHM, gzip, and RAR are corrected. Review
existing MIME allowlists and callers that previously supplied only filenames.
Use the new actions for machine-readable errors and mismatch/policy handling.

`MimeTypeService` and the original `FileType` .NET constructors remain public
for source compatibility but are **not ODC actions**. `MaximumStartLocation`
means the inclusive upper search offset; the new minimum/maximum constructor
supports fixed offsets when both values are equal. `FileValidator`, container
inspection, and MIME mappings are internal implementation details.

## Build and tests

Prerequisite: .NET 8 SDK for the existing `net8.0` target; NuGet access for restore.

```sh
dotnet restore MimeTypeDetection.sln
dotnet build MimeTypeDetection.sln -c Release
dotnet test MimeTypeDetection.sln -c Release
```

The xUnit test project uses deterministic in-memory fixtures and assertions.
It covers signatures/offsets, renamed files, in-memory Office packages, RIFF,
OLE ambiguity, archive corruption/limits, unsupported/empty/truncated content,
no extension fallback, validation policies, and the attributed API shape.
Synthetic fixtures test identification, not whether Microsoft Office can open
the package. The console project under `MimeTypeDetection.Tester/` is an old
manual example, not automated coverage; it is excluded from production builds.
`ODC/` contains excluded historical copies, not a second deployment source.

Local verification on Linux with SDK **8.0.428**: **128 tests passed, none failed
or skipped**; Release solution build completed with **0 warnings and 0 errors**;
framework-dependent `linux-x64` publish succeeded. These checks do not replace
ODC import/runtime testing or real-document interoperability testing.

## ODC packaging and deployment guidance

The sole production NuGet dependency remains
`OutSystems.ExternalLibraries.SDK` **1.5.0**. No native, Office automation, or
third-party MIME parsing dependencies were added. Test packages are test-only.
The API uses `OSInterface`, `OSAction`, `OSStructure`, public structure fields,
Text (`string`), Boolean (`bool`), Long Integer (`long`), Binary Data (`byte[]`),
and attributed nested structures. No streams, dictionaries, nullable primitives,
tasks, or enums cross the ODC action boundary.

Following the official template's Linux/framework-dependent workflow:

```sh
dotnet publish MimeTypeDetection.csproj -c Release -f net8.0 \
  -r linux-x64 --self-contained false -o artifacts/publish
```

ZIP the **contents** of `artifacts/publish` (DLLs, dependency metadata, etc.) at
the ZIP root, not the enclosing folder. Do not package the repository, test DLLs,
console tester, .NET runtime, or historical `ODC/` copies. For example, in PowerShell:

```powershell
Compress-Archive -Path ./artifacts/publish/* -DestinationPath ./artifacts/MimeTypeDetection.zip
```

When approved for release, upload that ZIP as an External Library in the ODC
Portal, inspect the imported actions/structures, and publish a version using
your tenant's normal workflow. Refresh application dependencies and smoke-test
real uploads in a nonproduction app, including rejection cases. **This change
does not publish or deploy anything.**

### Verified evidence versus outstanding checks

- SDK 1.5.0 includes `net8.0` assets and has no transitive dependencies. The
  official current template targets `net8.0;net10.0` and uses the same SDK.
- Local compilation, tests, API metadata checks, and Linux framework-dependent
  publish are useful evidence, **not proof of ODC importer/runtime support**.
- No ODC tenant/import/publishing execution was available. Confirm runtime
  availability, Binary Data marshaling (including empty inputs), nested structure
  import, platform payload/memory/time limits, dependency refresh compatibility,
  and real-document behavior in the target tenant before release.
- **Runtime lifecycle gate:** Microsoft lists .NET 8 end of support as
  **November 10, 2026**. The original target is retained to avoid an unannounced
  runtime migration. Before production release, confirm OutSystems' supported
  runtime policy and plan/test migration to .NET 10 (including SDK/package and
  packaging checks) for tenants that support it. Do not infer ongoing runtime
  support from NuGet compatibility or a successful .NET 8 build.

References (reviewed November 11, 2026):

- [ODC external logic documentation](https://www.outsystems.com/goto/external-logic)
- [Official SDK templates](https://github.com/OutSystems/OutSystems.ExternalLibraries.SDK-templates)
- [Official basic template and packaging script](https://github.com/OutSystems/OutSystems.ExternalLibraries.SDK-templates/tree/main/templates/basic/OutSystems.IbanChecker)
- [SDK 1.5.0 package/framework metadata](https://www.nuget.org/packages/OutSystems.ExternalLibraries.SDK/1.5.0)
- [Microsoft .NET support lifecycle](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core)
