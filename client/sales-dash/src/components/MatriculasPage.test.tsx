import { sortFilteredMatriculas } from './MatriculasPage';
import { UserMatricula } from '../services/apiService';

describe('sortFilteredMatriculas', () => {
  it('should prioritize owners (isOwner === true) before non-owners', () => {
    const items: UserMatricula[] = [
      {
        id: 1,
        userId: 'u1',
        userName: 'Carlos',
        matriculaNumber: '100',
        startDate: '2026-01-01',
        isActive: true,
        isOwner: false,
        status: 'Active',
        createdAt: '2026-01-01',
      },
      {
        id: 2,
        userId: 'u2',
        userName: 'Ana',
        matriculaNumber: '100',
        startDate: '2026-01-01',
        isActive: true,
        isOwner: true,
        status: 'Active',
        createdAt: '2026-01-01',
      },
      {
        id: 3,
        userId: 'u3',
        userName: 'Bruno',
        matriculaNumber: '100',
        startDate: '2026-01-01',
        isActive: true,
        isOwner: false,
        status: 'Active',
        createdAt: '2026-01-01',
      },
    ];

    const sorted = sortFilteredMatriculas(items);

    expect(sorted.map(s => s.userName)).toEqual(['Ana', 'Bruno', 'Carlos']);
    expect(sorted[0].isOwner).toBe(true);
    expect(sorted[1].isOwner).toBe(false);
    expect(sorted[2].isOwner).toBe(false);
  });

  it('should secondary sort by matriculaNumber numeric/alphanumeric order and then by userName', () => {
    const items: UserMatricula[] = [
      {
        id: 1,
        userId: 'u1',
        userName: 'Zack',
        matriculaNumber: '200',
        startDate: '2026-01-01',
        isActive: true,
        isOwner: true,
        status: 'Active',
        createdAt: '2026-01-01',
      },
      {
        id: 2,
        userId: 'u2',
        userName: 'Alice',
        matriculaNumber: '100',
        startDate: '2026-01-01',
        isActive: true,
        isOwner: true,
        status: 'Active',
        createdAt: '2026-01-01',
      },
      {
        id: 3,
        userId: 'u3',
        userName: 'Bob',
        matriculaNumber: '100',
        startDate: '2026-01-01',
        isActive: true,
        isOwner: false,
        status: 'Active',
        createdAt: '2026-01-01',
      },
      {
        id: 4,
        userId: 'u4',
        userName: 'Abby',
        matriculaNumber: '200',
        startDate: '2026-01-01',
        isActive: true,
        isOwner: false,
        status: 'Active',
        createdAt: '2026-01-01',
      },
    ];

    const sorted = sortFilteredMatriculas(items);

    // Owners first (matricula 100 Alice, matricula 200 Zack), then non-owners (matricula 100 Bob, matricula 200 Abby)
    expect(sorted.map(s => `${s.matriculaNumber}-${s.userName}-${s.isOwner}`)).toEqual([
      '100-Alice-true',
      '200-Zack-true',
      '100-Bob-false',
      '200-Abby-false',
    ]);
  });
});
