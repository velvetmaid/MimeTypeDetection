# Historical copies — not build inputs

This directory contains the original duplicated source/template files. The active
library is `../MimeTypeDetection.csproj`; it explicitly excludes `ODC/**/*.cs`.
The solution and tester reference the root library, not these copies.

These files are retained as historical reference, not maintained implementations.
Do not compile or package them. See the root README for the supported API and
packaging workflow. No separate ODC implementation is required.
