import React, { useState, useEffect, useCallback } from 'react';
import './UsersPage.css';
import Menu from './Menu';
import UserForm from './UserForm';
import { apiService, User, CreateUserRequest, UpdateUserRequest } from '../services/apiService';

const UsersPage: React.FC = () => {
  const [users, setUsers] = useState<User[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [page, setPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const [search, setSearch] = useState('');
  const [searchDebounce, setSearchDebounce] = useState('');
  const [showForm, setShowForm] = useState(false);
  const [editingUser, setEditingUser] = useState<User | undefined>(undefined);
  const [deleteConfirm, setDeleteConfirm] = useState<string | null>(null);
  const pageSize = 10;

  // Fetch users
  const fetchUsers = useCallback(async () => {
    try {
      setLoading(true);
      setError('');
      const response = await apiService.getUsers(page, pageSize, searchDebounce || undefined);
      
      if (response.success && response.data) {
        setUsers(response.data.items);
        setTotalCount(response.data.totalCount);
        setTotalPages(Math.ceil(response.data.totalCount / pageSize));
      }
    } catch (err: any) {
      setError(err.message || 'Failed to load users');
    } finally {
      setLoading(false);
    }
  }, [page, searchDebounce]);

  // Debounce search input
  useEffect(() => {
    const timer = setTimeout(() => {
      setSearchDebounce(search);
      setPage(1); // Reset to first page on search
    }, 500);

    return () => clearTimeout(timer);
  }, [search]);

  // Call fetchUsers when page or searchDebounce changes
  useEffect(() => {
    fetchUsers();
  }, [fetchUsers]);


  const handleCreateUser = async (userData: CreateUserRequest) => {
    await apiService.createUser(userData);
    setShowForm(false);
    fetchUsers();
  };

  const handleUpdateUser = async (userData: UpdateUserRequest) => {
    if (editingUser) {
      await apiService.updateUser(editingUser.id, userData);
      setShowForm(false);
      setEditingUser(undefined);
      fetchUsers();
    }
  };

  const handleDeleteUser = async (id: string) => {
    try {
      await apiService.deleteUser(id);
      setDeleteConfirm(null);
      fetchUsers();
    } catch (err: any) {
      setError(err.message || 'Failed to delete user');
    }
  };

  const openEditForm = (user: User) => {
    setEditingUser(user);
    setShowForm(true);
  };

  const closeForm = () => {
    setShowForm(false);
    setEditingUser(undefined);
  };

  const formatDate = (dateString: string) => {
    return new Date(dateString).toLocaleDateString('pt-BR', {
      day: '2-digit',
      month: '2-digit',
      year: 'numeric',
    });
  };

  const getRoleBadgeClass = (role: string) => {
    switch (role.toLowerCase()) {
      case 'superadmin':
        return 'role-badge role-superadmin';
      case 'admin':
        return 'role-badge role-admin';
      default:
        return 'role-badge role-user';
    }
  };

  return (
    <div className="users-layout">
      <Menu />
      <div className="users-content">
        <div className="users-container">
          <div className="users-header">
            <div>
              <h1 className="users-title">Gerenciamento de Usuários</h1>
              <p className="users-subtitle">
                {totalCount} {totalCount === 1 ? 'usuário' : 'usuários'} cadastrado{totalCount === 1 ? '' : 's'}
              </p>
            </div>
            <button className="btn-create" onClick={() => setShowForm(true)}>
              + Criar Usuário
            </button>
          </div>

          <div className="search-bar">
            <input
              type="text"
              placeholder="Buscar por nome ou email..."
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              className="search-input"
            />
          </div>

          {error && <div className="error-banner">{error}</div>}

          {loading ? (
            <div className="loading-container">
              <div className="spinner"></div>
              <p>Carregando usuários...</p>
            </div>
          ) : users.length === 0 ? (
            <div className="empty-state">
              <p>Nenhum usuário encontrado</p>
              {search && <button onClick={() => setSearch('')} className="btn-clear-search">Limpar busca</button>}
            </div>
          ) : (
            <>
              <div className="table-container">
                <table className="users-table">
                  <thead>
                    <tr>
                      <th>Nome</th>
                      <th>Email</th>
                      <th>Função</th>
                      <th>Status</th>
                      <th>Criado em</th>
                      <th>Ações</th>
                    </tr>
                  </thead>
                  <tbody>
                    {users.map((user) => (
                      <tr key={user.id}>
                        <td>
                          <div className="user-name-cell">
                            <span className="user-name">{user.name}</span>
                            {user.parentUserName && (
                              <span className="user-parent">Pai: {user.parentUserName}</span>
                            )}
                          </div>
                        </td>
                        <td>{user.email}</td>
                        <td>
                          <span className={getRoleBadgeClass(user.role)}>
                            {user.role === 'superadmin' ? 'Super Admin' : 
                             user.role === 'admin' ? 'Admin' : 'Usuário'}
                          </span>
                        </td>
                        <td>
                          <span className={`status-badge ${user.isActive ? 'status-active' : 'status-inactive'}`}>
                            {user.isActive ? 'Ativo' : 'Inativo'}
                          </span>
                        </td>
                        <td>{formatDate(user.createdAt)}</td>
                        <td>
                          <div className="action-buttons">
                            <button
                              className="btn-edit"
                              onClick={() => openEditForm(user)}
                              title="Editar"
                            >
                              ✏️
                            </button>
                            <button
                              className="btn-delete"
                              onClick={() => setDeleteConfirm(user.id)}
                              title="Excluir"
                            >
                              🗑️
                            </button>
                          </div>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>

              {totalPages > 1 && (
                <div className="pagination">
                  <button
                    className="pagination-btn"
                    onClick={() => setPage(p => Math.max(1, p - 1))}
                    disabled={page === 1}
                  >
                    ← Anterior
                  </button>
                  <span className="pagination-info">
                    Página {page} de {totalPages}
                  </span>
                  <button
                    className="pagination-btn"
                    onClick={() => setPage(p => Math.min(totalPages, p + 1))}
                    disabled={page === totalPages}
                  >
                    Próxima →
                  </button>
                </div>
              )}
            </>
          )}
        </div>
      </div>

      {showForm && (
        <UserForm
          user={editingUser}
          onSubmit={editingUser ? handleUpdateUser : handleCreateUser}
          onCancel={closeForm}
          isEdit={!!editingUser}
        />
      )}

      {deleteConfirm && (
        <div className="modal-overlay" onClick={() => setDeleteConfirm(null)}>
          <div className="confirm-dialog" onClick={(e) => e.stopPropagation()}>
            <h3>Confirmar Exclusão</h3>
            <p>Tem certeza que deseja excluir este usuário? Esta ação irá desativá-lo.</p>
            <div className="confirm-actions">
              <button className="btn-cancel" onClick={() => setDeleteConfirm(null)}>
                Cancelar
              </button>
              <button className="btn-confirm-delete" onClick={() => handleDeleteUser(deleteConfirm)}>
                Excluir
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};

export default UsersPage;
