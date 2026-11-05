export enum Segment {
  Lar = 1,
  Motores = 2,
  Servicos = 3,
}

export const SegmentLabels: Record<Segment, string> = {
  [Segment.Lar]: 'Lar',
  [Segment.Motores]: 'Motores',
  [Segment.Servicos]: 'Serviços',
};

export const getSegmentLabel = (segment?: number | null): string => {
  if (!segment) return '';
  return SegmentLabels[segment as Segment] || String(segment);
};
