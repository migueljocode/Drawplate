# Third-party notices

Drawplate includes or depends on the following third-party software.

## .NET runtime / SDK

Drawplate is published as a self-contained Native AOT .NET 10 application. It includes the .NET runtime, licensed under the MIT license (https://dotnet.microsoft.com).

## NuGet packages

| Package | License | Purpose |
|---------|---------|---------|
| xunit | MIT | Unit test framework |
| xunit.runner.visualstudio | MIT | Test runner adapter |
| Microsoft.NET.Test.Sdk | MIT | Test SDK |
| coverlet.collector | MIT | Code coverage |

## FFmpeg (not distributed)

Drawplate does not download, bundle, or host FFmpeg. FFmpeg is installed and managed by the user. When FFmpeg is built with libx264 (e.g. the Gyan full build or Homebrew formula), FFmpeg is licensed under the GNU GPL version 2 or later, with the GNU Lesser General Public License version 2.1 or later applying to libx264 and certain other components. See https://ffmpeg.org/legal.html.

Drawplate invokes FFmpeg as a separate process; it does not link against FFmpeg libraries.

## Source

Drawplate source: https://github.com/migueljocode/Drawplate (MIT license)
