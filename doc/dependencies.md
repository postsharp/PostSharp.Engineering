# Metalama Dependency Graph

```mermaid
graph BT

    Metalama.Compiler --> Metalama.Backstage
    Metalama --> Metalama.Compiler
    Metalama.Try --> Metalama
    Metalama.Try -.-> Metalama.Samples
    Metalama.Samples --> Metalama
    Metalama.Documentation --> Metalama
    Metalama.Documentation -.-> Metalama.Try
    Metalama.Open.ALL --> Metalama
    Metalama.Vsx --> Metalama
```