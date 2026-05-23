# EDI & Supply Chain Gateway for In & Out Promotions

## 1. Overview
The **EDI & Supply Chain Gateway** is an enterprise-grade integration middleware designed to orchestrate electronic data exchange (EDI) with suppliers, specifically for high-priority temporary "In & Out" promotional campaigns in retail environments.

It automates procurement, tracks shipping notifications, and manages warehouse slots to ensure on-time delivery for time-critical promotional windows.

## 2. Key Features
* **Campaign Tracking Dashboard:** Monitor fulfillment and delivery status of promotional campaigns.
* **PO Processing:** Automate outbound EDI document generation (EDIFACT `ORDERS`).
* **Inbound Message Parsing:** Handle `ORDRSP` (Order Response) and `DESADV` (Despatch Advice) messages.
* **Warehouse Slot Management:** Integrate with WMS to coordinate truck arrival slots.
* **Proactive Alerting:** Flag missing responses, shipping delays, or quantity discrepancies.

## 3. Technology Stack
* **Framework:** .NET 8 (ASP.NET Core MVC)
* **Database:** PostgreSQL with Entity Framework Core (EF Core)
* **Observability:** OpenTelemetry (Prometheus metrics, Grafana logs/traces)
* **Architecture:** Clean Architecture (Core, Application, Infrastructure, Web layers)
* **Logging:** Serilog with structured logging

## 4. Getting Started

### Prerequisites
* **.NET 8 SDK** (Required for building and running)
* **PostgreSQL** (Database system)

### Installation & Setup
1. **Restore dependencies:**
   ```powershell
   dotnet restore RetailPromoEdiGateway.sln
   ```

2. **Build the solution:**
   ```powershell
   dotnet build RetailPromoEdiGateway.sln
   ```

3. **Run tests:**
   ```powershell
   dotnet test RetailPromoEdiGateway.sln
   ```

4. **Run the application:**
   ```powershell
   dotnet run --project src\RetailPromoEdiGateway.Web
   ```

## 5. Project Structure
* `src/RetailPromoEdiGateway.Core`: Domain entities, enums, and core business rules.
* `src/RetailPromoEdiGateway.Application`: Use cases (MediatR), interfaces, and application logic.
* `src/RetailPromoEdiGateway.Infrastructure`: Database implementation (EF Core), external services, and background processors.
* `src/RetailPromoEdiGateway.Web`: MVC/API Controllers, Views, and application configuration.
* `tests/`: Unit and integration tests.

## 6. Architecture & Data Flow

### 6.1 Clean Architecture Dependency Flow
The project is built following Clean Architecture principles, ensuring separation of concerns, testability, and independence from external frameworks:

```mermaid
graph TD
    %% Clean Architecture Layering
    subgraph Presentation ["Presentation Layer"]
        Web[RetailPromoEdiGateway.Web]
    end
    
    subgraph Infra ["Infrastructure Layer"]
        EFCore[EF Core PostgreSQL DbContext]
        EdiServices[EDIFACT Parser & Service Client]
        OTel[OpenTelemetry Stack]
    end

    subgraph App ["Application Layer"]
        MediatR[MediatR Handlers & CQRS]
        DTOs[DTOs & Use Cases]
        FluentVal[FluentValidators]
    end

    subgraph Core ["Core / Domain Layer"]
        Entities[Domain Entities & Value Objects]
        Interfaces[Repository & Service Interfaces]
    end

    %% Dependency Directions (Inner layers do not depend on outer layers)
    Web --> App
    Web --> Infra
    Infra -.-> Interfaces
    App --> Core
```

### 6.2 End-to-End EDI and Supply Chain Flow
The gateway orchestrates communication between the internal ERP system, external suppliers, and the Warehouse Management System (WMS):

```mermaid
sequenceDiagram
    autonumber
    participant ERP as ERP / PIM
    participant GW as EDI Gateway (.NET 8)
    participant SUP as External Supplier
    participant WMS as WMS System

    Note over ERP, SUP: 1. Purchase Order Dispatch
    ERP->>GW: POST /api/v1/orders (Promotional PO)
    GW->>GW: Persist PO & Queue Outbox Message
    GW->>SUP: Dispatch EDIFACT ORDERS (Purchase Order)

    Note over GW, SUP: 2. Supplier Acknowledgment
    SUP->>GW: POST /api/v1/edi/inbound (ORDRSP)
    GW->>GW: Parse & Validate quantities/dates
    alt Discrepancy Found
        GW->>GW: Flag Mismatched & Trigger Alert
    else Validation Successful
        GW->>GW: Update PO Status to Confirmed
    end

    Note over SUP, WMS: 3. Advanced Shipping & Slot Booking
    SUP->>GW: POST /api/v1/edi/inbound (DESADV / ASN)
    GW->>GW: Process Shipped SSCC & Pallets
    GW->>WMS: POST /api/v1/logistics/slots (Request Slot Booking)
    WMS-->>GW: Booked Slot Confirmation (Bay & Arrival Time)
    GW->>GW: Associate booked slot with DESADV
```
