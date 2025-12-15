# AggregationSummary Component

A reusable presentational component for displaying contract aggregation metrics.

## Props

| Prop | Type | Description |
|------|------|-------------|
| `total` | `number` | Total sum of all contract amounts |
| `totalCancel` | `number` | Total sum of defaulted contract amounts |
| `retention` | `number` | Retention rate (0.0 to 1.0) |

## Usage

```tsx
import AggregationSummary from '../shared/AggregationSummary';

<AggregationSummary
  total={15000}
  totalCancel={2500}
  retention={0.75}
/>
```

## Features

- **NaN Handling**: Displays `--` for invalid values (NaN, null, undefined)
- **Currency Formatting**: Brazilian Real (R$) format for monetary values
- **Percentage Display**: Retention shown as percentage with 1 decimal place
- **Color Coding**: 
  - Total: Green
  - Total Cancelado: Red
  - Taxa de Retenção: Blue

## Display

Shows three metrics in a responsive grid:
- **Total Geral**: R$ 15.000,00
- **Total Cancelado**: R$ 2.500,00
- **Taxa de Retenção**: 75.0%
