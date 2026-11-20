import { RtCellStatus, RtCurrentStatus } from '../services/careerPlanService'

export interface SimulatedContractState {
  rts: RtCellStatus[]
  current: RtCurrentStatus
  simulatedStatus: string
  overdueMonths: number
  paidMonths: number
}

/**
 * Pure function: Computes the simulated state when an individual RT cell (colIndex: 0..5) is clicked.
 * - RT1 (colIndex 0): Fixo como verde. Never changes.
 * - RT2..RT6 (colIndex 1..5):
 *   - If currently 'paid': turns 'unpaid' (and subsequent active cells also turn 'unpaid').
 *   - If currently 'unpaid': turns 'paid' (and preceding cells also turn 'paid').
 *   - If currently 'blank': activates cell and preceding cells up to colIndex as 'paid'.
 *   - Overdue count is mapped to status: 0 -> Active, 1 -> Late1, 2 -> Late2, 3 -> Late3, 4+ -> Defaulted.
 *   - RT Atual is 'upToDate' (green) if 0 overdue, 'overdue' (red) if >= 1 overdue.
 */
export function computeNextRtCellState(
  originalRts: readonly RtCellStatus[],
  originalCurrent: RtCurrentStatus,
  currentSimulated: SimulatedContractState | undefined,
  colIndex: number
): SimulatedContractState {
  // Rule: RT1 is strictly fixed as green
  if (colIndex <= 0) {
    return (
      currentSimulated ?? {
        rts: [...originalRts],
        current: originalCurrent,
        simulatedStatus: originalCurrent === 'overdue' ? 'Late1' : 'Active',
        overdueMonths: originalRts.filter(r => r === 'unpaid').length,
        paidMonths: originalRts.filter(r => r === 'paid').length
      }
    )
  }

  const effectiveRts: RtCellStatus[] = [...(currentSimulated?.rts ?? originalRts)]
  const currentCell = effectiveRts[colIndex]

  if (currentCell === 'paid') {
    // Turning cell into unpaid (overdue).
    // Rule: "Atrasado acumulado no final"
    // Cell colIndex and any subsequent active cells (paid/unpaid) become unpaid.
    for (let i = colIndex; i < effectiveRts.length; i++) {
      if (effectiveRts[i] !== 'blank') {
        effectiveRts[i] = 'unpaid'
      }
    }
    // Prior cells are guaranteed paid
    for (let i = 0; i < colIndex; i++) {
      if (effectiveRts[i] === 'unpaid') {
        effectiveRts[i] = 'paid'
      }
    }
  } else if (currentCell === 'unpaid') {
    // Turning cell into paid.
    // Cell colIndex and all prior cells become paid.
    for (let i = 0; i <= colIndex; i++) {
      effectiveRts[i] = 'paid'
    }
  } else {
    // Cell is blank (future month simulation).
    // Activate cell and any preceding blank cells as paid.
    for (let i = 0; i <= colIndex; i++) {
      if (effectiveRts[i] === 'blank') {
        effectiveRts[i] = 'paid'
      }
    }
  }

  // Always ensure RT1 is paid
  effectiveRts[0] = 'paid'

  return deriveSimulationState(effectiveRts)
}

/**
 * Pure function: Computes the simulated state when RT Atual is clicked.
 * - If current is 'overdue': turns all 'unpaid' cells into 'paid' (RT Atual -> 'upToDate', Status -> 'Active').
 * - If current is 'upToDate': turns the most recent active/due cell into 'unpaid' (RT Atual -> 'overdue', Status -> 'Late1').
 */
export function computeNextCurrentCellState(
  originalRts: readonly RtCellStatus[],
  originalCurrent: RtCurrentStatus,
  currentSimulated: SimulatedContractState | undefined
): SimulatedContractState {
  const effectiveCurrent = currentSimulated?.current ?? originalCurrent
  const effectiveRts: RtCellStatus[] = [...(currentSimulated?.rts ?? originalRts)]

  if (effectiveCurrent === 'overdue') {
    // Turn all unpaid cells into paid (contract 100% up to date)
    for (let i = 0; i < effectiveRts.length; i++) {
      if (effectiveRts[i] === 'unpaid') {
        effectiveRts[i] = 'paid'
      }
    }
  } else {
    // Turn from upToDate into overdue: mark the most recent active cell as unpaid
    let lastActiveIndex = -1
    for (let i = effectiveRts.length - 1; i >= 1; i--) {
      if (effectiveRts[i] === 'paid') {
        lastActiveIndex = i
        break
      }
    }
    // If only RT1 is paid, activate RT2 as unpaid
    const targetIndex = lastActiveIndex >= 1 ? lastActiveIndex : 1
    effectiveRts[targetIndex] = 'unpaid'
  }

  // Always ensure RT1 is paid
  effectiveRts[0] = 'paid'

  return deriveSimulationState(effectiveRts)
}

/**
 * Pure helper: derives overdueMonths, paidMonths, current and status from an array of RTs.
 */
export function deriveSimulationState(rts: readonly RtCellStatus[]): SimulatedContractState {
  const overdueMonths = rts.filter(r => r === 'unpaid').length
  const paidMonths = rts.filter(r => r === 'paid').length
  const current: RtCurrentStatus = overdueMonths > 0 ? 'overdue' : 'upToDate'

  let simulatedStatus = 'Active'
  if (overdueMonths === 1) simulatedStatus = 'Late1'
  else if (overdueMonths === 2) simulatedStatus = 'Late2'
  else if (overdueMonths === 3) simulatedStatus = 'Late3'
  else if (overdueMonths >= 4) simulatedStatus = 'Defaulted'

  return {
    rts: [...rts],
    current,
    simulatedStatus,
    overdueMonths,
    paidMonths
  }
}

/**
 * Pure function: Checks whether a simulated state differs from the original contract row.
 */
export function isContractStateModified(
  originalRts: readonly RtCellStatus[],
  originalCurrent: RtCurrentStatus,
  simulated: SimulatedContractState
): boolean {
  if (simulated.current !== originalCurrent) return true
  if (simulated.rts.length !== originalRts.length) return true
  for (let i = 0; i < originalRts.length; i++) {
    if (simulated.rts[i] !== originalRts[i]) return true
  }
  return false
}
