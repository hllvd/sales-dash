# AggregationSummary Component

A reusable presentational component for displaying contract aggregation metrics with visual chart.

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
- **Visual Chart**: Donut chart showing retention vs defaulted distribution
- **Color Coding**: 
  - Total: Green (#22c55e)
  - Total Cancelado: Red (#ef4444)
  - Taxa de Retenção: Blue (#3b82f6)

## Chart Visualization

The component includes a donut chart that visualizes:
- **Retidos** (Green): Percentage of retained contracts
- **Inadimplentes** (Red): Percentage of defaulted contracts

Chart only displays when retention data is valid (not NaN/null/undefined).

## Display

Shows three metrics in a responsive grid plus chart:
- **Total Geral**: R$ 15.000,00
- **Total Cancelado**: R$ 2.500,00
- **Taxa de Retenção**: 75.0%
- **Chart**: Visual representation of retention (75%) vs defaulted (25%)

## Dependencies

- `@mantine/charts`: For DonutChart component
- `recharts`: Chart rendering library (peer dependency)
