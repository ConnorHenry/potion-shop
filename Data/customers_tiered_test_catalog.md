# Early Customer Test Catalog

This catalog is mirrored by `Data/customers_tiered_test_data.tres`.

Most entries in this catalog are available from day 1. The deterministic day-two opener is explicitly gated to day 2. The requests are built around the two-trait ingredient preparation model:

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
| `plot_bridget_visit_1` | None | None | Legacy-gated Bridget welcome scene with happy/sad portrait keys. |
| `customer_requests_opening_gravekeepers_balm` | None | Exact Minor Healing Potion | Deterministic first shop customer for new games; Mother asks for the renamed tutorial potion brewed from starting raw Mint, Gorse, and Thyme. |
| `customer_requests_opening_silver_focus_tonic` | Courage 8, Vigor 3, Clarity 2 | None | Deterministic second shop customer for new games; arrival grants Comfrey, Willow, and Yarrow. |
| `customer_requests_opening_clean_vigor_tonic` | Cleanse 7, Soothe 4, Vigor 3 | None | Deterministic third shop customer for new games; arrival restocks Mint, Gorse, Thyme, Comfrey, Willow, and Yarrow to at least 5 each. |
| `customer_requests_day_two_charmed_focus_tonic` | Courage 8, Charm 4, Vigor 3 | None | Deterministic first customer on day 2. |
| `customer_requests_day_two_crowded_head_tonic` | Hidden: Cleanse 7, Soothe 5, Clarity 4 | Enables Boiled prep method when served | Deterministic second customer on day 2; desired request details display as `?????`. |
| `customer_requests_day_two_rest_memory_clarity` | Rest 5+, Memory 5+, Clarity 4+ | No insomnia or melancholy | Deterministic third customer on day 2. |
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
| Warmth | Heather |
| Dream | Rosemary |
| Soothe | Mint, Gorse |
| Cleanse | Mint, Thyme |
| Rest | Elder, Thyme |
| Vigor | Elder, Yarrow |
| Mend | Gorse |
| Discipline | Comfrey |
| Charm | Juniper |
| Luck | Yarrow |
| Clarity | Willow, Juniper |
| Memory | Willow |
| Courage | Comfrey |
