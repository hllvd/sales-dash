export enum ContractType {
  Lar = "lar",
  Motores = "motores"
}

export const ContractTypeLabels: Record<ContractType, string> = {
  [ContractType.Lar]: "LAR",
  [ContractType.Motores]: "Motores"
}
