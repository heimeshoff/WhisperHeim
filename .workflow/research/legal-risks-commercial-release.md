# Research: Legal Risks of Commercial Release

**Date:** 2026-03-26
**Status:** Complete
**Relevance:** Commercial launch viability, liability management, regulatory compliance

## Summary

Three areas were investigated: (1) liability for the recording/wiretapping capability, (2) liability for voice cloning/TTS, and (3) general German/EU legal requirements for selling software. The core finding is that for recording, the **user is liable, not the developer** — provided the app is a general-purpose local tool and the developer never accesses the data (Sony Betamax doctrine). For voice cloning, the risk is **higher and evolving** — the EU AI Act requires machine-readable watermarking of synthetic audio by August 2026, and Germany's Berlin court (2025) ruled AI voice imitations violate personality rights. For general business obligations, the new EU Product Liability Directive (effective December 2026) explicitly makes software subject to strict liability, and "as-is" disclaimers are void in German consumer contracts.

**Bottom line:** Form a UG (haftungsbeschraenkt) before releasing. Get IT-Haftpflicht insurance. Implement consent warnings for recording, consent verification + watermarking for voice cloning, and draft a lawyer-reviewed EULA.

## Key Findings

### 1. Recording / Wiretapping — Developer Liability is Low

**The user is liable, not the tool maker**, under the Sony Betamax doctrine (1984): a manufacturer is not liable for users' infringing acts if the product has "substantial lawful uses." A desktop recording app clearly qualifies (podcasts, consented meetings, accessibility, language learning).

**Critical distinction from recent lawsuits:** Cloud recording services (Otter.ai, RingCentral, Cresta) are being sued because they act as third-party interceptors who receive, process, and store the audio. A purely local app that never transmits data occupies a fundamentally different legal position — closer to a VCR than a cloud AI service.

**German law (StGB §201):** Recording non-public speech without consent is criminal (up to 3 years). Developer liability as Gehilfe (accessory, §27 StGB) requires double intent — knowing about a specific criminal act and intending to support it. Providing a general-purpose tool does not meet this threshold.

**Recommended protections:**
- First-run disclaimer about recording consent requirements
- Pre-recording reminder before each system audio capture session
- Visible "Recording" indicator during capture
- ToS/EULA placing compliance responsibility on the user (industry standard — Otter.ai, Fireflies.ai, tl;dv all use this)
- Indemnification clause

| Jurisdiction | Consent Model | Developer Risk |
|---|---|---|
| US Federal | One-party | Very Low |
| US (CA, FL, IL, etc.) | All-party | Very Low (user responsibility) |
| Germany | All-party (§201 StGB) | Low (Beihilfe requires specific intent) |
| EU (GDPR) | Depends on legal basis | Very Low if data stays local |

### 2. Voice Cloning / TTS — Moderate and Rising Risk

This is the area with the most legal movement. Three threat vectors:

#### EU AI Act — Article 50 Transparency (Deadline: August 2, 2026)

Voice cloning is classified as **"limited risk"** under the EU AI Act. Obligations for providers (developers):
- Must ensure synthetic audio outputs are **marked in a machine-readable format** and detectable as AI-generated
- Must be "effective, interoperable, robust, and reliable as far as technically feasible"
- **No exemption for local/offline software** — applies if the system is placed on the EU market
- Penalties: up to **15 million EUR or 3% of global turnover** for transparency violations

#### German Personality Rights (Persoenlichkeitsrecht)

**Landmark case: LG Berlin, August 20, 2025 (2 O 202/24):** A YouTuber used AI to clone voice actor Manfred Lehmann's dubbing voice. Court ruled this violates personality rights. Ordered 4,000 EUR in licensing fees. Established that AI voice imitations carry the same legal liability as human impersonations.

This case targeted the **user**, not the tool maker. But a pending criminal law (draft §201b StGB from the Bundesrat, July 2024) would criminalize creation AND distribution of realistic synthetic media without consent — up to 2 years imprisonment.

#### US State Laws

**Tennessee ELVIS Act (effective July 2024):** Explicitly creates liability for anyone who "distributes an algorithm or technology whose primary purpose or function" involves creating unauthorized voice facsimiles. This directly targets tool makers.

**NO FAKES Act (pending federal):** Would impose liability on tool distributors "primarily designed" for unauthorized replicas, with a safe harbor for tools whose primary purpose is something else. A general-purpose TTS app with cloning as a feature likely falls under the safe harbor, but it's uncertain.

#### Recommended Protections

1. **Consent verification for voice cloning:** At minimum, require users to confirm they have consent (checkbox + acknowledgment). Gold standard: require a recorded consent statement in the target voice + biometric voice matching (Microsoft's approach).
2. **Machine-readable watermarking in all synthetic audio** — required by EU AI Act by August 2026. Consider C2PA metadata standards.
3. **Terms of service** explicitly prohibiting unauthorized voice cloning, impersonation, and fraud
4. **Consider limiting cloning to the user's own voice** — the safest legal position
5. **Audit logging** of voice cloning operations (timestamps, voice profiles, output files)

| Risk Area | Risk Level | Notes |
|---|---|---|
| EU AI Act Art. 50 transparency | **HIGH** | Must implement watermarking by Aug 2026 |
| German personality rights | **MEDIUM-HIGH** | Users can be sued; developer risk less clear but rising |
| US state right-of-publicity | **MEDIUM** | ELVIS Act targets tool makers explicitly |
| Federal IP claims | **LOW** | Dismissed in Lehrman v. Lovo (2024) |

### 3. German Business & Legal Requirements

#### Business Structure

**Form a UG (haftungsbeschraenkt) before releasing.** Minimum capital: 1 EUR (practically ~500-1,000 EUR setup with notary). Provides personal liability protection — essential given the new EU Product Liability Directive.

An Einzelunternehmen (sole proprietorship) exposes you to unlimited personal liability. With the Product Liability Directive making software subject to strict (no-fault) liability starting December 2026, this is an unacceptable risk.

#### EU Product Liability Directive 2024/2853 (Effective December 9, 2026)

**This is the biggest legal change for software developers:**
- **Software is explicitly a "product"** subject to strict liability
- **"As-is" disclaimers are invalid** — cannot contractually exclude liability
- **No maximum liability cap** (the old directive had one)
- **Failure to provide security updates** can itself constitute a defect
- Compensation covers psychological harm and data destruction
- Open source exemption only applies if NOT provided commercially

#### EULA / AGB — What's Enforceable in Germany

Many standard US EULA clauses are **void** in German consumer contracts:

| Clause | Status in Germany |
|---|---|
| "Software provided AS-IS" | **Void** |
| "No liability for any damages" | **Void** |
| "Binding arbitration / class action waiver" | **Void** |
| "We may change terms at any time" | **Void** |
| "No refunds" | **Void** |
| "Governed by California law" | **Void** for German consumers |
| Liability cap to purchase price | **Partially OK** — only for simple negligence on non-essential obligations |

**What you CAN do:** Detailed product description (your best liability tool — sets expectations), customer cooperation duties (backups, error reporting), and limit liability for simple negligence on non-essential obligations to foreseeable damages.

#### Digital Content Directive (Already in Force)

**Update obligation:** You must provide updates (including security updates) to keep the product in conformity. For one-time purchases, the duration is "as long as consumers can reasonably expect" — deliberately vague. For subscriptions, it ends when the subscription ends.

**Warranty period:** 1-year burden-of-proof reversal — for the first year, YOU must prove the product was not defective.

#### VAT / Sales

- **Kleinunternehmerregelung** (small business exemption): Up to 25,000 EUR/year previous revenue, 100,000 EUR current year — no VAT charged
- EU B2C digital sales require VAT at buyer's country rate — use **OSS (One-Stop-Shop)** or a merchant of record (Paddle, LemonSqueezy) to handle this
- **Strongly recommend a merchant of record** to handle VAT, invoicing, and refunds across all countries

#### Mandatory Website Requirements

- **Impressum** (§5 DDG): Full name, physical address, phone/email, trade register number. Fines up to 50,000 EUR for missing Impressum.
- **Datenschutzerklaerung** (privacy policy): Required even if the app collects zero data — your website processes IP addresses
- **Cookie consent**: Only needed if using non-essential cookies (analytics, tracking). Skip all tracking for simplicity and privacy credibility.

#### Insurance

**IT-Haftpflichtversicherung** (IT professional liability): ~300-600 EUR/year for indie developers. Covers software defects causing financial loss. Providers: Hiscox, exali, AXA. **Essential** given the Product Liability Directive.

## Action Items Summary

| Priority | Action | Cost | Deadline |
|---|---|---|---|
| **Critical** | Form UG (haftungsbeschraenkt) | ~500-1,000 EUR | Before release |
| **Critical** | Lawyer-reviewed EULA (German/EU-compliant) | ~500-2,000 EUR | Before release |
| **Critical** | Recording consent disclaimers in app | Dev time | Before release |
| **Critical** | Voice cloning consent verification | Dev time | Before release |
| **High** | IT-Haftpflichtversicherung | ~300-600 EUR/year | Before release |
| **High** | Impressum + privacy policy on website | Free-300 EUR | Before release |
| **High** | Merchant of record for EU VAT | 5-10% of revenue | Before first sale |
| **High** | Machine-readable watermarking of TTS output | Dev time | Before August 2, 2026 |
| **Medium** | Gewerbeanmeldung | ~30 EUR | Before release |
| **Medium** | Third-party license notices file | Dev time | Before release |

## Commercial vs Free/Open Source — Legal Comparison

Releasing as free, open source software under the Unlicense dramatically reduces legal exposure. Here is how each risk area shifts:

### What Goes Away (Free/Open Source)

**EU Product Liability Directive 2024/2853:** Explicit open source exemption — software excluded if "not provided in the course of a business activity." Free + non-commercial + no revenue = exemption applies. No strict liability, no voided "as-is" disclaimers, no uncapped damages.

**Digital Content Directive / §327 BGB:** Only applies when digital content is provided in exchange for payment or personal data. No charge + no data collection = does not apply. No warranty obligations, no mandatory updates, no 1-year burden-of-proof reversal.

**Business requirements:** No Gewerbeanmeldung, no UG/GmbH needed, no VAT/OSS, no merchant of record, no IT-Haftpflichtversicherung required. Impressum may still be required for the website in simplified form.

**EULA/AGB consumer protection:** No consumer contract = German consumer protection rules (§§305-310 BGB) do not bite. The Unlicense itself disclaims all liability, and without a commercial relationship there is no contract to enforce warranty through.

### What Stays the Same

**EU AI Act Article 50 (watermarking, deadline Aug 2, 2026):** The open source exemption in Article 2(12) explicitly does NOT exempt Article 50 transparency obligations. Even free, open source synthetic audio tools must implement machine-readable watermarking. This obligation survives regardless of business model.

**Recording / wiretapping liability:** The Sony Betamax analysis is identical — developer is not liable either way. The tool has substantial lawful uses regardless of whether it is commercial.

**German personality rights (Persoenlichkeitsrecht):** Tort law claims exist regardless of commercial status. Someone could theoretically claim the tool enabled a personality rights violation.

### What Changes in Practice

**Enforcement reality:** Nobody sues a hobby open source developer with zero revenue. Litigation is expensive, and there is nothing to recover from an individual not running a business. Practical risk drops from "moderate" to "near-zero" across almost every category.

**EU AI Act enforcement:** The penalties (up to 15M EUR) technically still apply, but regulators will focus on commercial actors. No enforcement action against non-commercial open source AI tools has occurred to date.

### Side-by-Side Comparison

| Risk Area | Commercial (Proprietary) | Free (Unlicensed) |
|---|---|---|
| Product Liability Directive | Strict liability, no cap, no "as-is" | **Exempt** |
| Warranty / updates (§327 BGB) | Mandatory, 1-year burden reversal | **Does not apply** |
| EU AI Act Art. 50 watermarking | Required | **Still required** (but practically unenforced) |
| Recording liability | Low (user's problem) | Low (user's problem) |
| Voice cloning tort claims | Medium | **Low** (no profitable target) |
| Business registration needed | Yes (UG + insurance + VAT) | **No** |
| Upfront legal cost | ~2,000-5,000 EUR | ~0 EUR |
| Revenue potential | Real (MacWhisper: ~300K copies) | Zero |

### Assessment

Going open source + free eliminates almost all legal overhead and risk. The only obligation that technically survives is EU AI Act watermarking for synthetic audio — and even that is practically unenforced against open source projects.

The trade-off is clear: ~2,000-5,000 EUR upfront legal cost for going commercial buys access to a proven revenue opportunity (MacWhisper demonstrated millions in revenue with this exact app category), while free/open source costs nothing but generates nothing.

## Open Questions

- Should voice cloning be limited to own-voice only? (Safest legally, but limits the feature)
- Which watermarking standard for synthetic audio? (C2PA is the leading candidate)
- How long is a "reasonable" update obligation for a one-time purchase? (No case law yet)
- Should the EULA be drafted in German, English, or both?
- At what revenue level does the Kleinunternehmerregelung become impractical?

## Sources

### Recording / Wiretapping
- [AI Recording Tools Trigger Lawsuits (FKKS)](https://technologylaw.fkks.com/post/102kz0c/ai-recording-notetaking-tools-trigger-wave-of-lawsuits-could-your-business-be)
- [Applying Anti-Wiretapping Laws to AI Transcription (IAPP)](https://iapp.org/news/a/dressing-old-laws-in-class-action-suits-applying-anti-wiretapping-laws-to-ai-transcription-services)
- [Otter.ai Terms of Service](https://otter.ai/terms-of-service)
- [StGB §201 (official)](https://www.gesetze-im-internet.de/stgb/__201.html)
- [Germany Recording Laws (RecordingLaw.com)](https://recordinglaw.com/germany-recording-laws/)
- [Sony Corp. v. Universal, 464 U.S. 417 (1984)](https://supreme.justia.com/cases/federal/us/464/417/)
- [50-State Recording Laws Survey (Justia)](https://www.justia.com/50-state-surveys/recording-phone-calls-and-conversations/)
- [EDPB Guidelines 02/2021 on Virtual Voice Assistants](https://www.edpb.europa.eu/system/files/2021-07/edpb_guidelines_202102_on_vva_v2.0_adopted_en.pdf)

### Voice Cloning / TTS
- [Lehrman v. Lovo — Court Dismisses IP Claims (Fredrikson)](https://www.fredlaw.com/alert-federal-court-dismisses-trademark-and-copyright-claims-over-ai-voice-clones-but-leaves-door-open-under-state-publicity-law)
- [ELVIS Act (Holland & Knight)](https://www.hklaw.com/en/insights/publications/2024/04/first-of-its-kind-ai-law-addresses-deep-fakes-and-voice-clones)
- [NO FAKES Act H.R.2794](https://www.congress.gov/bill/119th-congress/house-bill/2794/text)
- [EU AI Act Article 50](https://artificialintelligenceact.eu/article/50/)
- [EU AI Act and Voice Cloning (Soundverse)](https://www.soundverse.ai/blog/article/eu-ai-act-and-voice-cloning-regulations-explained-1055)
- [German Court: AI Voice Cloning Violates Personality Rights](https://ppc.land/german-court-rules-ai-voice-cloning-violates-personality-rights/)
- [AI Voice Imitation & German Law (SE Legal)](https://se-legal.de/ai-voice-imitation-personality-rights-german-law/?lang=en)
- [Germany Bundesrat Deepfake Draft §201b (DataGuidance)](https://www.dataguidance.com/news/germany-bundesrat-publishes-draft-law-deepfakes)
- [Consumer Reports: Voice Cloning Product Assessment](https://www.consumerreports.org/media-room/press-releases/2025/03/consumer-reports-assessment-of-ai-voice-cloning-products/)
- [Microsoft Custom Neural Voice Consent](https://learn.microsoft.com/en-us/azure/ai-services/speech-service/professional-voice-create-consent)

### German Business / EU Law
- [EU Product Liability Directive 2024/2853 (EUR-Lex)](https://eur-lex.europa.eu/eli/dir/2024/2853/oj/eng)
- [PLD: Implications for Software (Reed Smith)](https://www.reedsmith.com/articles/eu-product-liability-directive-software-digital-products-cybersecurity/)
- [PLD: Liability for Software (IBA)](https://www.ibanet.org/European-Product-Liability-Directive-liability-for-software)
- [Digital Content Directive 2019/770 (EUR-Lex)](https://eur-lex.europa.eu/eli/dir/2019/770/oj/eng)
- [Germany: New Rules on Digital Products (Reed Smith)](https://www.reedsmith.com/en/perspectives/2022/01/germany-new-rules-on-digital-products-and-consumer-rights)
- [Software-Gewaehrleistung (Verbraucherzentrale)](https://www.verbraucherzentrale.de/wissen/vertraege-reklamation/kundenrechte/softwaregewaehrleistung-welche-rechte-habe-ich-bei-fehlenden-updates-74911)
- [Liability Exclusions under German Law (Taylor Wessing)](https://www.taylorwessing.com/de/insights-and-events/insights/2018/06/liability-exclusions-under-german-law)
- [Limitation of Liability in Contracts (ITMediaLaw)](https://itmedialaw.com/en/wissensdatenbank/limitation-of-liability-in-contracts/)
- [Impressum Requirements (IONOS)](https://www.ionos.com/digitalguide/websites/digital-law/a-case-for-thinking-global-germanys-impressum-laws/)
- [Website Compliance Germany (All About Berlin)](https://allaboutberlin.com/guides/website-compliance-germany)
- [Kleinunternehmer 2026 (Norman Finance)](https://norman.finance/blog/kleinunternehmer)
- [VAT Changes Germany 2025 (vat-germany.com)](https://www.vat-germany.com/blog/vat-changes-in-germany-2025-update-for-international-businesses/)
- [IT-Haftpflichtversicherung (Hiscox)](https://www.hiscox.de/geschaeftskunden/it-haftpflicht-versicherung/)
- [IT-Haftpflichtversicherung Vergleich (gewerbeversicherung.de)](https://www.gewerbeversicherung.de/versicherungen/haftpflicht/it-haftpflichtversicherung-2/)
- [UG vs GmbH (Norman Finance)](https://norman.finance/blog/ug-vs-gmbh)
