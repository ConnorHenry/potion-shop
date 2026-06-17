# Day 1 Customer Test Catalog

This catalog is mirrored by `Data/customers_tiered_test_data.tres`.

All entries in this catalog are available from day 1. The requests are built around the two-trait ingredient preparation model:

- each request uses trait ranges from the current ingredient matrix
- each request uses risk ceilings from current prep risks
- requests avoid hard `requiredIngredientAmounts`
- requests are intentionally solvable through multiple ingredient and preparation combinations

## Request Shape

Customer request traits use range objects:

```json
"desiredTraits": {
  "calm": { "min": 3, "max": 6 },
  "clarity": { "min": 3, "max": 6 },
  "cleanse": { "min": 2, "max": 5 }
},
"badTraits": {
  "drowsiness": { "max": 0 },
  "corruption": { "max": 0 }
}
```

For requests with three desired traits, the customer sale rules require at least two desired traits to fall inside their ranges. This leaves room for multiple successful recipes while still making the customer's intent readable.

## Available Customers

| Customer ID | Desired Traits | Risk Limits | Notes |
| --- | --- | --- | --- |
| `plot_bridget_visit_1` | None | None | Bridget welcome scene with happy/sad portrait keys. |
| `plot_line_demo_visit_1` | Soothe, clarity, courage | No drowsiness, melancholy up to 1 | Plot customer with dialogue tree. |
| `customer_requests_counterfeit_calm` | Calm, clarity, cleanse | No drowsiness or corruption | Multiple calm/clarity sources can solve it. |
| `customer_requests_clean_blade_rinse` | Cleanse, soothe, clarity | No drowsiness or corruption | Cleanse can come from Mint or Thyme preps. |
| `customer_requests_soft_dream_tonic` | Dream, calm, soothe | No drowsiness, melancholy up to 1 | Dream can be reached through Heather or Rosemary preps. |
| `customer_requests_grave_stitch_poultice` | Mend, soothe, cleanse | Melancholy up to 1, no corruption | Mending can come from Gorse or Comfrey preps. |
| `customer_requests_stage_door_spark` | Charm, vigor, calm | No corruption, insomnia up to 1 | Charm can come from Yarrow or Juniper preps. |
| `customer_requests_bitter_wake_cure` | Vigor, cleanse, clarity | Insomnia up to 1, no drowsiness | Vigor can come from Elder or Yarrow preps. |
| `customer_requests_quiet_courage_draught` | Courage, clarity, calm | Melancholy up to 1, no corruption | Courage can come from Willow or Comfrey preps. |
| `customer_requests_restless_fever_draught` | Rest, cleanse, soothe | No corruption, drowsiness up to 1 | Rest can come from Elder or Thyme preps. |
| `customer_requests_silver_invitation` | Charm, clarity, courage | No corruption, melancholy up to 1 | Charm plus either clarity or courage has multiple routes. |
| `customer_requests_lantern_wash` | Soothe, cleanse, courage | No drowsiness, melancholy up to 1 | Soothe/cleanse can be built from Mint, Gorse, or Thyme preps. |

## Ingredient Trait Sources

| Trait | Ingredient Sources |
| --- | --- |
| Calm | Heather, Rosemary |
| Dream | Heather, Rosemary |
| Soothe | Mint, Gorse |
| Cleanse | Mint, Thyme |
| Rest | Elder, Thyme |
| Vigor | Elder, Yarrow |
| Mend | Gorse, Comfrey |
| Charm | Yarrow, Juniper |
| Clarity | Willow, Juniper |
| Courage | Willow, Comfrey |
