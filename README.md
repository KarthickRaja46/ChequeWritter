# Cheque Writer

A small Windows program for printing **RAK Bank (Business)** cheques, with fils (decimals),
amount in words, a cheque register and a payee list.

## Features

- **Print cheques** at exact millimetre positions: date (one character per box), payee,
  amount in figures, amount in words over two lines, A/C Payee Only crossing, and "or Bearer" crossed out.
- **Decimals / fils**: `4523.20` prints as *Four Thousand Five Hundred Twenty Three And Fils Twenty Only*.
- **Cheque register**: every printed cheque is saved. You can search it, filter by date, mark cheques void, add notes,
  reload a cheque to reprint it, and export to Excel (CSV).
- **Payee list** with auto-complete. New payees are added automatically when you print.
- **Layout editor**: drag any field, or nudge it with the arrow keys (0.1 mm, or 1 mm with Shift). You can also set the
  printer offset and load a scan of a blank cheque as the background.
- **Test print on plain paper** draws the cheque outline and every field box, so you can hold it over a real cheque.
- Backup and restore of all data.

Only the RAK Business layout is included for now.

## Using it

1. Type the cheque number, date, payee and amount. The preview updates as you type.
2. Click **Test print on plain paper** once and hold the sheet over a real cheque against the light.
   - If everything is off by the same amount, go to **Tools > Layout editor > Cheque & printer settings** and change
     **Offset X / Offset Y** (mm).
   - If only one field is off, drag that field in the layout editor.
3. Put the cheque in the printer, pushing it against the paper guide the same way every time, and press **Print cheque** (Ctrl+P).

The page is A4 landscape with the cheque in the top-left corner, the same way the RAK layout was calibrated.

## Where the data is kept

`%APPDATA%\ChequeWriter\chequewriter-data.json`. It is one JSON file holding the layout, payees, register and settings.
Each save keeps the previous version as `.bak`.

**Portable mode:** put an empty file named `portable.txt` next to `ChequeWriter.exe`. The data then lives in a
`Data` folder beside the exe, for example on a USB stick.

## Building

Requires the .NET 8 SDK.

```bat
dotnet test          :: run the unit tests
publish.bat          :: makes publish\ChequeWriter.exe (single file, no .NET install needed on the target PC)
```

Project layout:

| Folder | What it is |
|---|---|
| `src/ChequeWriter.Core` | Models, RAK layout, amount in words, text wrapping, JSON storage, CSV export (no UI) |
| `src/ChequeWriter` | WinForms app: main window, renderer, printing, layout editor, register, payees |
| `tests/ChequeWriter.Tests` | xUnit tests for words, wrapping, storage and CSV |

## RAK Business layout

All positions are in mm from the top-left of the page (`DefaultLayouts.RakBusiness()`):

| Field | X | Y | Width | Font |
|---|---|---|---|---|
| Date (`dd-MM-yyyy`, 3.16 mm per character) | 156.23 | 18.83 | 32 | 9.75 pt |
| Payee | 37.22 | 36.14 | 130 | 11.25 pt |
| Amount in words, line 1 | 16.50 | 45.31 | 118.12 | 11.25 pt |
| Amount in words, line 2 | 7.06 | 55.00 | 134 | 11.25 pt |
| Amount in figures | 157.67 | 45.70 | 40 | 11.25 pt |
| "or Bearer" strike (`XXXXXXX`) | 5.45 | 38.35 | 17 | 9 pt |
| A/C PAYEE ONLY (rotated 35°) between two crossing lines | 3.70 | 1.30 | 34 | 8.25 pt |

These were confirmed with a test print on a real RAK Business cheque in Nov 2026. In that test the amount figure
had to move up 1.6 mm to sit inside the box.
