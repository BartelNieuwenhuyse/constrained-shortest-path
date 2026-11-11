# Changelog

All notable changes to this project will be documented in this file.

## [Unreleased]
### Added
- Initial repository setup and project structure organized into source, benchmarks and documentation
- Core data structures and graph utilities
- Benchmarking project and input generation utilities for Delay Constrained Shortest Path on layered DAGs (DCSP on lDAGs)
- Reference solvers for DCSP on lDAGs:
    - Depth first label setting with pruning on feasibility, weight bound and dominance (Pulse style algorithm)
    - Depth first label setting with pruning on feasiblity, weight bound and Lagrangian bound (LRE style algorithm)
- Novel solvers for DCSP on lDAGs:
    - Depth first label setting with backward pruning on feasibility, weight bound and dominance
    - Depth first label setting with pruning on delay completion equivalence
    - Depth first label setting with pruning on weight completion equivalence

### Changed

### Fixed
