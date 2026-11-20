import {
  computeNextRtCellState,
  computeNextCurrentCellState,
  isContractStateModified
} from './ContractRtSimulationLogic'
import { RtCellStatus, RtCurrentStatus } from '../services/careerPlanService'

describe('ContractRtSimulationLogic', () => {
  describe('computeNextRtCellState', () => {
    it('RT1 is fixed as green and never toggles to unpaid', () => {
      const originalRts: RtCellStatus[] = ['paid', 'paid', 'blank', 'blank', 'blank', 'blank']
      const originalCurrent: RtCurrentStatus = 'upToDate'

      const next = computeNextRtCellState(originalRts, originalCurrent, undefined, 0)
      expect(next.rts[0]).toBe('paid')
      expect(next.current).toBe('upToDate')
    })

    it('Example 1: R1 green, R2 yellow, Current red -> clicking R2 turns R2 green and Current green', () => {
      const originalRts: RtCellStatus[] = ['paid', 'unpaid', 'blank', 'blank', 'blank', 'blank']
      const originalCurrent: RtCurrentStatus = 'overdue'

      const next = computeNextRtCellState(originalRts, originalCurrent, undefined, 1)

      expect(next.rts[0]).toBe('paid')
      expect(next.rts[1]).toBe('paid')
      expect(next.current).toBe('upToDate')
      expect(next.simulatedStatus).toBe('Active')
      expect(next.overdueMonths).toBe(0)
      expect(next.paidMonths).toBe(2)
      expect(isContractStateModified(originalRts, originalCurrent, next)).toBe(true)
    })

    it('Example 2: R1 green, R2 green, Current green -> clicking R2 turns R2 yellow and Current red', () => {
      const originalRts: RtCellStatus[] = ['paid', 'paid', 'blank', 'blank', 'blank', 'blank']
      const originalCurrent: RtCurrentStatus = 'upToDate'

      const next = computeNextRtCellState(originalRts, originalCurrent, undefined, 1)

      expect(next.rts[0]).toBe('paid')
      expect(next.rts[1]).toBe('unpaid')
      expect(next.current).toBe('overdue')
      expect(next.simulatedStatus).toBe('Late1')
      expect(next.overdueMonths).toBe(1)
      expect(next.paidMonths).toBe(1)
    })

    it('accumulates overdue at the end when marking earlier installment as unpaid', () => {
      const originalRts: RtCellStatus[] = ['paid', 'paid', 'paid', 'paid', 'blank', 'blank']
      const originalCurrent: RtCurrentStatus = 'upToDate'

      // Click RT2 (colIndex 1) to mark as unpaid -> RT2, RT3, RT4 become unpaid
      const next = computeNextRtCellState(originalRts, originalCurrent, undefined, 1)

      expect(next.rts[0]).toBe('paid')
      expect(next.rts[1]).toBe('unpaid')
      expect(next.rts[2]).toBe('unpaid')
      expect(next.rts[3]).toBe('unpaid')
      expect(next.rts[4]).toBe('blank')
      expect(next.overdueMonths).toBe(3)
      expect(next.simulatedStatus).toBe('Late3')
      expect(next.current).toBe('overdue')
    })

    it('allows clicking a blank cell to activate future months as paid', () => {
      const originalRts: RtCellStatus[] = ['paid', 'paid', 'blank', 'blank', 'blank', 'blank']
      const originalCurrent: RtCurrentStatus = 'upToDate'

      // Click RT3 (colIndex 2)
      const next = computeNextRtCellState(originalRts, originalCurrent, undefined, 2)

      expect(next.rts[0]).toBe('paid')
      expect(next.rts[1]).toBe('paid')
      expect(next.rts[2]).toBe('paid')
      expect(next.rts[3]).toBe('blank')
      expect(next.overdueMonths).toBe(0)
      expect(next.paidMonths).toBe(3)
      expect(next.current).toBe('upToDate')
    })

    it('detects when simulated state is identical to original state', () => {
      const originalRts: RtCellStatus[] = ['paid', 'paid', 'blank', 'blank', 'blank', 'blank']
      const originalCurrent: RtCurrentStatus = 'upToDate'

      // Toggle R2 to unpaid
      const sim1 = computeNextRtCellState(originalRts, originalCurrent, undefined, 1)
      expect(isContractStateModified(originalRts, originalCurrent, sim1)).toBe(true)

      // Toggle R2 back to paid
      const sim2 = computeNextRtCellState(originalRts, originalCurrent, sim1, 1)
      expect(isContractStateModified(originalRts, originalCurrent, sim2)).toBe(false)
    })
  })

  describe('computeNextCurrentCellState', () => {
    it('clicking overdue RT Atual turns all unpaid cells to paid (100% upToDate)', () => {
      const originalRts: RtCellStatus[] = ['paid', 'unpaid', 'unpaid', 'blank', 'blank', 'blank']
      const originalCurrent: RtCurrentStatus = 'overdue'

      const next = computeNextCurrentCellState(originalRts, originalCurrent, undefined)

      expect(next.rts[0]).toBe('paid')
      expect(next.rts[1]).toBe('paid')
      expect(next.rts[2]).toBe('paid')
      expect(next.current).toBe('upToDate')
      expect(next.simulatedStatus).toBe('Active')
      expect(next.overdueMonths).toBe(0)
    })

    it('clicking upToDate RT Atual turns last active cell into unpaid (overdue)', () => {
      const originalRts: RtCellStatus[] = ['paid', 'paid', 'paid', 'blank', 'blank', 'blank']
      const originalCurrent: RtCurrentStatus = 'upToDate'

      const next = computeNextCurrentCellState(originalRts, originalCurrent, undefined)

      expect(next.rts[0]).toBe('paid')
      expect(next.rts[1]).toBe('paid')
      expect(next.rts[2]).toBe('unpaid')
      expect(next.current).toBe('overdue')
      expect(next.simulatedStatus).toBe('Late1')
      expect(next.overdueMonths).toBe(1)
    })
  })
})
