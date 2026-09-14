# Fallback fonts

Noto Sans Regular and Bold are used only when the platform's UI font is absent.
They are embedded to support headless systems without installed fonts.

Source: https://github.com/notofonts/noto-fonts/tree/main/hinted/ttf/NotoSans
License: SIL Open Font License 1.1 (see LICENSE.txt, distributed with the CLI).
No proprietary font is embedded. Windows uses its installed Segoe UI;
macOS prefers its system UI face, then Helvetica Neue; Linux prefers DejaVu Sans
or Noto Sans. Font rasterization and available fallback glyphs may differ by OS.

SHA-256 of the unmodified downloaded files:

- Regular: `B85C38ECEA8A7CFB39C24E395A4007474FA5A4FC864F6EE33309EB4948D232D5`
- Bold: `C976E4B1B99EDC88775377FCC21692CA4BFA46B6D6CA6522BFDA505B28FF9D6A`
