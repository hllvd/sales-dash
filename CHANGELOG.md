# Changelog

All notable changes to this project will be documented in this file.

## [Unreleased]

### Added
- PowerBI Scraper integration with dynamic user credentials support.
- Centralized `ScrapeResult` model in C# API.
- Automated CSV import trigger upon successful scrape completion.
- Mock testing utility for scraper logic verification.

### Fixed
- Typo in `ScrapeController` that caused compilation issues.
- Missing `ScrapeConfigs` database table and seeded test data.
