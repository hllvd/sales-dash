import React, { useState } from "react"
import "./UserForm.css"
import { User } from "../services/apiService"

interface UserFormProps {
  user?: User
  onSubmit: (userData: any) => Promise<void>
  onCancel: () => void
  isEdit?: boolean
}

const UserForm: React.FC<UserFormProps> = ({
  user,
  onSubmit,
  onCancel,
  isEdit = false,
}) => {
  const [formData, setFormData] = useState({
    name: user?.name || "",
    email: user?.email || "",
    password: "",
    role: user?.role || "user",
    parentUserId: user?.parentUserId || "",
    isActive: user?.isActive ?? true,
  })
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState("")

  const handleChange = (
    e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement>
  ) => {
    const { name, value, type } = e.target
    const checked = (e.target as HTMLInputElement).checked

    setFormData((prev) => ({
      ...prev,
      [name]: type === "checkbox" ? checked : value,
    }))
  }

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    setError("")
    setLoading(true)

    try {
      const userData: any = {
        name: formData.name,
        email: formData.email,
        role: formData.role,
      }

      if (formData.password) {
        userData.password = formData.password
      }

      if (formData.parentUserId) {
        userData.parentUserId = formData.parentUserId
      }

      if (isEdit) {
        userData.isActive = formData.isActive
      }

      await onSubmit(userData)
    } catch (err: any) {
      setError(err.message || "An error occurred")
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="modal-overlay" onClick={onCancel}>
      <div className="modal-content" onClick={(e) => e.stopPropagation()}>
        <div className="modal-header">
          <h2>{isEdit ? "Editar Usuário" : "Criar Novo Usuário"}</h2>
          <button className="close-button" onClick={onCancel}>
            ×
          </button>
        </div>

        <form onSubmit={handleSubmit} className="user-form">
          {error && <div className="error-message">{error}</div>}

          {isEdit && (
            <div className="form-group">
              <label htmlFor="id">ID do Usuário</label>
              <input
                type="text"
                id="id"
                name="id"
                value={user?.id || ""}
                readOnly
                disabled
                placeholder="ID do usuário"
              />
            </div>
          )}

          <div className="form-group">
            <label htmlFor="name">Nome *</label>
            <input
              type="text"
              id="name"
              name="name"
              value={formData.name}
              onChange={handleChange}
              required
              maxLength={100}
              placeholder="Nome completo"
            />
          </div>

          <div className="form-group">
            <label htmlFor="email">Email *</label>
            <input
              type="email"
              id="email"
              name="email"
              value={formData.email}
              onChange={handleChange}
              required
              placeholder="email@exemplo.com"
            />
          </div>

          <div className="form-group">
            <label htmlFor="password">
              Senha {!isEdit && "*"}
              {isEdit && (
                <span className="hint">
                  (deixe em branco para manter a atual)
                </span>
              )}
            </label>
            <input
              type="password"
              id="password"
              name="password"
              value={formData.password}
              onChange={handleChange}
              required={!isEdit}
              minLength={6}
              placeholder="Mínimo 6 caracteres"
            />
          </div>

          <div className="form-group">
            <label htmlFor="role">Função *</label>
            <select
              id="role"
              name="role"
              value={formData.role}
              onChange={handleChange}
              required
            >
              <option value="user">Usuário</option>
              <option value="admin">Administrador</option>
              <option value="superadmin">Super Administrador</option>
            </select>
          </div>

          <div className="form-group">
            <label htmlFor="parentUserId">
              ID do Usuário Pai
              <span className="hint">(opcional)</span>
            </label>
            <input
              type="text"
              id="parentUserId"
              name="parentUserId"
              value={formData.parentUserId}
              onChange={handleChange}
              placeholder="UUID do usuário pai"
            />
          </div>

          {isEdit && (
            <div className="form-group checkbox-group">
              <label>
                <input
                  type="checkbox"
                  name="isActive"
                  checked={formData.isActive}
                  onChange={handleChange}
                />
                <span>Usuário Ativo</span>
              </label>
            </div>
          )}

          <div className="form-actions">
            <button
              type="button"
              onClick={onCancel}
              className="btn-cancel"
              disabled={loading}
            >
              Cancelar
            </button>
            <button type="submit" className="btn-submit" disabled={loading}>
              {loading
                ? "Salvando..."
                : isEdit
                ? "Salvar Alterações"
                : "Criar Usuário"}
            </button>
          </div>
        </form>
      </div>
    </div>
  )
}

export default UserForm
