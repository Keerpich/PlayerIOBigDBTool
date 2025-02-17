# PlayerIOTool
A tool for importing/exporting/purging data from PlayerIO BigDB

[![.NET](https://github.com/Keerpich/PlayerIOBigDBTool/actions/workflows/dotnet.yml/badge.svg?branch=main)](https://github.com/Keerpich/PlayerIOBigDBTool/actions/workflows/dotnet.yml)

This was based on https://github.com/atillabyte/PlayerIOExportTool

## Publish
In order to publish the .exe file use `dotnet publish -c Release --self-contained true` in the root folder.

## Usage
```bash
PlayerIOExportTool:
  A tool to assist with exporting a Player.IO game.

Usage:
  PlayerIOExportTool [options]

Options:
  --username <username>              The username of your Player.IO account
  --password <password>              The password of your Player.IO account
  --game-id <game-id>                The ID of the game to export. For example: tictactoe-vk6aoralf0yflzepwnhdvw
  --action <action>                  What action is expected from the tool: purge, push-to-server, pull-from-server
  --version                          Show version information
  -?, -h, --help                     Show help and usage information
```

## Disclaimer
THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
"AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
OWNER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

This software was designed to be used only for research purposes.
This software comes with no warranties of any kind whatsoever,
and may not be useful for anything.  Use it at your own risk!
If these terms are not acceptable, you aren't allowed to use the code.
