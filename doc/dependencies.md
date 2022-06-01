# Metalama Dependency Graph

```mermaid
graph BT

    Metalama.Compiler --> Metalama.Backstage
    Metalama --> Metalama.Compiler
    Metalama.Try.Tester --> Metalama
    Metalama.Try.Web --> Metalama
    Metalama.Try.Web ---> Metalama.Samples
    Metalama.Samples --> Metalama
    Metalama.Samples --> Metalama.Try.Tester
    Metalama.Documentation --> Metalama
    Metalama.Documentation --> Metalama.Try.Tester
    Metalama.Documentation -.-> Metalama.Try.Web
    Metalama.Open.ALL --> Metalama
    Metalama.Vsx --> Metalama
```