# Localization Process for TuWasGutes

## Overview
All user-facing text in both the frontend and backend is managed in resource files for each supported language (currently English and German). No user-visible text should be hardcoded in HTML, JavaScript, or C# code.

## Frontend (Web)
- All UI strings are stored in `/src/OneGood.Api/wwwroot/i18n/en.json` and `/src/OneGood.Api/wwwroot/i18n/de.json`.
- The frontend JavaScript dynamically loads the appropriate JSON file based on the user's browser language.
- All UI elements reference these translations using the `t(key)` function.
- To add or update a UI string:
  1. Add the key and value to both `en.json` and `de.json`.
  2. Reference the key in your JS or HTML using `t('yourKey')` or `data-i18n="yourKey"`.

## Backend (API)
- All user-facing API messages are stored in `.resx` resource files: `/src/OneGood.Core/Resources/ApiMessages.en.resx` and `/src/OneGood.Core/Resources/ApiMessages.de.resx`.
- Controllers and services reference these resources for all error, success, and info messages.
- To add or update an API message:
  1. Add the key and value to both `.resx` files.
  2. Reference the key in C# using the strongly-typed resource accessor (e.g., `ApiMessages.YourKey`).

## Adding a New Language
1. Copy `en.json` and `ApiMessages.en.resx` to new files for the target language (e.g., `fr.json`, `ApiMessages.fr.resx`).
2. Translate all values.
3. Update the frontend and backend to detect and use the new language as needed.

## Best Practices
- Never hardcode user-facing text in code or markup.
- Always keep all language files in sync.
- Use descriptive, consistent keys for all messages.
- Test all UI and API messages in all supported languages after changes.
