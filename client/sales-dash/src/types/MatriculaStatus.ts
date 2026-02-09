export enum MatriculaStatus {
  Active = "active",
  Pending = "pending"
}

export const MatriculaStatusLabels: Record<MatriculaStatus, string> = {
  [MatriculaStatus.Active]: "Ativo",
  [MatriculaStatus.Pending]: "Pendente"
}

export enum ActiveState {
  Active = "active",
  Inactive = "inactive"
}

export const ActiveStateLabels: Record<ActiveState, string> = {
  [ActiveState.Active]: "Ativa",
  [ActiveState.Inactive]: "Inativa"
}
