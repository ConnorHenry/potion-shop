# Tiered Customer Test Catalog

This catalog is mirrored by `Data/customers_tiered_test_data.tres`.

The current customer import schema supports `desiredTraits`, `badTraits`, day requirements, pool, weight, and difficulty. It does not yet support hard minimum trait thresholds, so Tier 3+ uses higher desired trait weights to simulate stricter requests for testing.

| Tier | Days | Design Goal | Customer IDs |
| --- | --- | --- | --- |
| 1 | 1-2 | Simple trait matching with no risk restrictions. | `tier1_sleep_draught`, `tier1_pain_relief_tonic`, `tier1_focus_elixir`, `tier1_steady_hands_tincture` |
| 2 | 3-4 | Introduce one rejected risk per customer. | `tier2_clean_sleep_draught`, `tier2_dawn_energy_tonic`, `tier2_clear_voice_balm`, `tier2_uncorrupted_charm_philter` |
| 3 | 5-6 | Simulate minimum trait expectations with stronger primary desired traits, plus risk restrictions. | `tier3_nightmare_ward`, `tier3_antidote_potion`, `tier3_confidence_draught`, `tier3_recovery_brew` |
| 4 | 7-8 | Named recipe-style requests with stronger trait expectations and multiple constraints. | `tier4_moonlit_rest_draught`, `tier4_gravekeepers_balm`, `tier4_silver_focus_tonic`, `tier4_orchid_charm_philter` |
| 5 | 9+ | Expert requests with high primary trait expectations and multiple risk restrictions. | `tier5_curse_cleanser`, `tier5_ravenheart_elixir`, `tier5_obsidian_mending_draught`, `tier5_nightshade_reverie` |

## Day Outline

| Day | Eligible Customers | Difficulty Feel |
| --- | --- | --- |
| 1 | Sleep Draught, Pain Relief Tonic, Focus Elixir | Match one obvious need. |
| 2 | Sleep Draught, Pain Relief Tonic, Focus Elixir, Steady Hands Tincture | Same rules, more coverage. |
| 3 | Clean Sleep Draught, Dawn Energy Tonic, Clear Voice Balm | First risk exclusions. |
| 4 | Clean Sleep Draught, Dawn Energy Tonic, Clear Voice Balm, Uncorrupted Charm Philter | Risk exclusions become normal. |
| 5 | Nightmare Ward, Antidote Potion, Confidence Draught | Strong primary trait expectations start. |
| 6 | Nightmare Ward, Antidote Potion, Confidence Draught, Recovery Brew | Strong primary traits plus multiple bad traits. |
| 7 | Moonlit Rest Draught, Gravekeeper's Balm, Silver Focus Tonic | Named recipe-style requests begin. |
| 8 | Moonlit Rest Draught, Gravekeeper's Balm, Silver Focus Tonic, Orchid Charm Philter | Recipe requests with more constraints. |
| 9 | Curse Cleanser, Ravenheart Elixir, Obsidian Mending Draught | Expert constraints. |
| 10+ | Curse Cleanser, Ravenheart Elixir, Obsidian Mending Draught, Nightshade Reverie | Full late-game pressure. |

## Future Schema Candidate

To make Tier 3+ enforce exact thresholds instead of simulating them through weights, add a field like this to customer data:

```json
"requiredTraits": {
  "clarity": 5
}
```

That would also require adding `RequiredTraits` to `CustomerInteractionDef`, parsing it in `DataDb`, carrying it into `CustomerRequestDef`, and checking it during potion sale evaluation.
