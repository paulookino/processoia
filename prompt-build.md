# ProcessIA — Build Prompt (Linus Torvalds Mode)

---

Listen up. I'm going to tell you exactly what we're building and how.
No fluff. No over-engineering. No "enterprise patterns" nobody asked for.
You write clean, working code or you don't write anything at all.

---

## What we're building

A SaaS that takes a scanned legal process PDF — the kind that's been sitting in a filing
cabinet for 20 years — and turns it into a structured report.

That's it. One job. Do it well.

Upload PDF → OCR → AI analysis → Structured report → Export.

If you start talking about microservices or event sourcing before we have a single
paying customer, I will close this conversation.

---

## Stack

**Backend:** ASP.NET Core 8 — Web API. No, we're not using anything else.
Ten years of .NET means we use .NET. Stop second-guessing yourself.

**OCR:** Azure Document Intelligence (FormRecognizer). Best OCR for scanned legal
documents. Don't use Tesseract unless you enjoy pain.

**AI Analysis:** Claude API (claude-sonnet-4-6). Feed it the extracted text,
get structured JSON back. Prompt engineering matters more than model choice here.

**Database:** PostgreSQL. Simple. Reliable. Not MongoDB. Never MongoDB for this.

**Auth:** ASP.NET Core Identity + JWT. Already solved. Don't reinvent it.

**Payments:** Stripe. Subscription billing, webhooks, portal. Done.

**Frontend:** Razor Pages or minimal React. I don't care which.
What I care about is that it works and loads fast.

**Hosting:** Azure App Service + Azure Blob Storage for PDF uploads.
Everything in one cloud because we're not playing DevOps hero with two providers.

---

## Data Model

Keep it stupid simple:

```
User
  - Id, Email, PasswordHash, CreatedAt
  - StripeCustomerId, SubscriptionStatus

Process (the uploaded legal document)
  - Id, UserId, FileName, BlobUrl
  - Status: Pending | Processing | Done | Failed
  - UploadedAt, ProcessedAt

Report (the output)
  - Id, ProcessId
  - Parties (JSON)
  - CaseValue (decimal)
  - CurrentPhase (string)
  - LastDecision (text)
  - NextDeadlines (JSON array)
  - RiskLevel: Low | Medium | High
  - RawExtractedText (text)
  - CreatedAt
```

That's your entire domain. If you add more than 8 tables before launch,
you've already made a mistake.

---

## The Pipeline (the only thing that matters)

```
1. User uploads PDF
   → Store in Azure Blob Storage
   → Create Process record (Status: Pending)
   → Queue background job

2. Background job (IHostedService or Hangfire)
   → Call Azure Document Intelligence
   → Get extracted text
   → Build prompt with extracted text
   → Call Claude API
   → Parse JSON response
   → Save Report to DB
   → Update Process status to Done
   → Notify user (email or real-time)

3. User sees report
   → Fetch from DB
   → Render structured view
   → Download PDF button
```

No magic. No complexity. A queue, a processor, a result. That's software.

---

## The Claude Prompt (this is your core IP)

```
You are a Brazilian legal document analyst with 20 years of experience.
You have been given the full text of a scanned legal process (processo judicial).
The text may have OCR errors — use context to interpret correctly.

Extract and return ONLY valid JSON with this exact structure:

{
  "parties": {
    "plaintiff": "name of the autor/requerente",
    "defendant": "name of the réu/requerido",
    "plaintiff_lawyer": "name if found, null otherwise",
    "defendant_lawyer": "name if found, null otherwise"
  },
  "case_value": 0.00,
  "current_phase": "brief description in Portuguese",
  "last_decision": "clear summary in plain Portuguese, no legalese, max 3 sentences",
  "next_deadlines": ["deadline 1", "deadline 2"],
  "risk_level": "LOW | MEDIUM | HIGH",
  "risk_justification": "one sentence explaining the risk classification"
}

Rules:
- If you cannot find a field, use null. Never hallucinate.
- last_decision must be written so a non-lawyer understands it.
- risk_level: HIGH if there's likely condemnation, MEDIUM if uncertain, LOW if favorable.
- Return ONLY the JSON. No explanation, no markdown, no preamble.
```

This prompt is worth more than your entire infrastructure.
Tune it with real documents before anything else.

---

## What you build first (MVP order)

1. PDF upload endpoint + Blob Storage → **Day 1**
2. Azure Document Intelligence integration → **Day 2**
3. Claude API call + JSON parsing → **Day 3**
4. Save report to DB, basic report view → **Day 4-5**
5. Auth (register/login) → **Day 6**
6. Stripe subscription + paywall → **Day 7-8**
7. PDF export of report → **Day 9-10**
8. Deploy to Azure → **Day 11**

Two weeks. That's your MVP. If it takes longer, you're over-engineering.

---

## What you do NOT build until you have 10 paying customers

- Dashboard with charts
- Team/multi-user features
- API for integrations
- Mobile app
- Batch processing UI
- Custom report templates
- White-label anything

I don't care how good those ideas sound. Zero customers means zero validation.
Build for the customer in front of you, not the one you imagined.

---

## Pricing (don't overthink it)

One plan: **R$297/month**. Unlimited analyses.

No free tier that bleeds your API costs.
No complex tier matrix that confuses the customer.
One price. One decision. Buy or don't buy.

Later you can add a higher tier. Not now.

---

## Error handling

Azure Document Intelligence fails? Retry twice, then mark as Failed, email the user.
Claude returns invalid JSON? Log it, retry once with a stricter prompt, then fail gracefully.
Blob upload fails? Tell the user immediately. Don't silently swallow it.

Errors happen. Handle them like an adult: log, retry, communicate, move on.

---

## Security (the non-negotiable list)

- Every file access checks UserId ownership. No exceptions.
- PDFs stored in private Blob container. Signed URLs with 1-hour expiry only.
- No raw SQL. Use EF Core parameterized queries. Always.
- HTTPS only. Not even a question.
- Stripe webhooks validated with signature. Every single one.

If you skip any of these, you deserve what happens next.

---

## Final note

You have ten years of .NET.
You know this stack cold.
Stop asking whether you should build this.

The market has lawyers reading paper processes by hand right now, today,
while you're reading this prompt.

Go write the code.

---

*"Talk is cheap. Show me the code."*
— L. Torvalds
