# Vocabula documentation

This directory is the source of truth for Vocabula's product and technical documentation. The documents are intentionally concise and should evolve with the implementation.

## Product

Vocabula helps language learners read interesting material that stays close to the vocabulary and grammar they have acquired. A learner submits photographs of studied material, reviews the extracted knowledge, and uses the approved knowledge to continue a personalized story.

The first product use case is a single learner studying Swedish. The account now carries a learning language (English, French, Spanish or Swedish) that switches the active learning profile: knowledge, story and chapters stay separate per language, while the daily chapter quota is shared across all of them.

### MVP scope

1. Accept a photograph of learning material.
2. Extract candidate vocabulary and grammar.
3. Let the learner review and correct the extraction.
4. Persist approved knowledge per learner and language.
5. Generate the next part of a story from new and previously acquired knowledge.
6. Preserve enough story context to continue the narrative.

### Current non-goals

- Microservices or Kubernetes
- Large-scale multi-region infrastructure
- Fully autonomous acceptance of AI-extracted knowledge
- Premature optimization for production-scale traffic

## Current technology

| Area | Current direction |
|---|---|
| Backend | C# / ASP.NET Core on .NET 10 |
| API exploration | OpenAPI / Swagger UI |
| AI provider | Mistral API |
| Current AI model | `mistral-small-2506` for extraction and generation |
| Local runtime | Rider and Docker Compose |
| Persistence direction | PostgreSQL locally; potentially Amazon RDS for PostgreSQL in AWS |
| Deployment direction | Container image in ECR, running on ECS Fargate (defined with Terraform, see [AWS deployment](architecture/aws-deployment.md)) |

Model names are implementation details and may change after quality and cost evaluation.

## Architecture principles

- Begin with a modular monolith and keep module boundaries explicit.
- Keep API containers stateless and replaceable.
- Persist durable data outside the API container.
- Treat AI extraction as a proposal until the learner approves it.
- Keep model providers replaceable and measure quality, latency, and cost.
- Use stable internal user IDs; treat email as a unique but changeable sign-in attribute.
- Store vocabulary as learner-acquired knowledge rather than attempting to build a global dictionary.
- Prefer the simplest architecture that validates the product.
- Record significant architectural choices and their cost consequences.

## Documents

- [Draft data model](architecture/data-model.md)
- [AWS deployment](architecture/aws-deployment.md)

Planned documents:

- `product/vision-and-scope.md`
- `architecture/system-context.md`
- `architecture/local-development.md`
- `ai/models-and-prompts.md`
- `decisions/` for Architecture Decision Records

## Documentation conventions

- Mermaid diagrams live inside Markdown so GitHub and Rider can render them.
- Diagrams describe intended design; EF Core migrations will describe the implemented physical schema.
- Open questions should remain visible until a decision is made.
- Never include API keys, credentials, private notebook images, or sensitive learner content.
