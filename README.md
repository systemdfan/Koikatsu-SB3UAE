# Koikatsu SB3U ADV Extractor
Simple (but still messy) Koikatsu ADV extractor using SB3U
Created for AI translation of Koikatsu for [KKIRV2](https://github.com/ilyabelka/KKIRV2_Patch) project

## Usage
```
koi_extract <abdata path> <output path> [config.ini] [--lang=] [--debug]
        --debug - display debug log
        --lang=language_code - extract specified language text (works only with kk party (doesn't work with most of the charachters))
        available codes:
                en-US
                cn-TW
                cn-CN
```

## Config.ini
Optional configuration. Maps replacement of name placeholders depending on folder of heroine
