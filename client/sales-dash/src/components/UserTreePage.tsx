import React, { useState, useEffect, useMemo } from "react"
import {
  Title,
  Text,
  Group,
  Card,
  NumberInput,
  TextInput,
  Loader,
  Badge,
  Avatar,
  ActionIcon,
  Tooltip,
  Tree,
  Select,
  RenderTreeNodePayload,
  TreeNodeData,
  useTree
} from "@mantine/core"
import {
  IconSearch,
  IconTree,
  IconEye,
  IconChevronRight,
  IconChevronDown,
  IconUsers
} from "@tabler/icons-react"
import "./UserTreePage.css"
import Menu from "./Menu"
import { apiService, UserHierarchyNode } from "../services/apiService"
import { UserProfileModal } from "./UserProfile"

const UserTreePage: React.FC = () => {
  const [users, setUsers] = useState<UserHierarchyNode[]>([])
  const [totalUsers, setTotalUsers] = useState(0)
  const [maxDepth, setMaxDepth] = useState(0)
  const [depth, setDepth] = useState<number | string>(10)
  const [search, setSearch] = useState("")
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState("")
  const [selectedProfileUserId, setSelectedProfileUserId] = useState<string | null>(null)
  
  const [isSuperAdmin, setIsSuperAdmin] = useState(false)
  const [availableUsers, setAvailableUsers] = useState<Array<{ value: string, label: string }>>([])
  const [selectedUserId, setSelectedUserId] = useState<string | null>(null)

  useEffect(() => {
    const user = JSON.parse(localStorage.getItem("user") || "{}")
    const superadmin = user.role === "superadmin"
    setIsSuperAdmin(superadmin)

    if (superadmin) {
      apiService.getUsers(1, 1000)
        .then(res => {
          if (res.success && res.data) {
            setAvailableUsers(
              res.data.items.map(u => ({
                value: u.id,
                label: `${u.name} (${u.email})`
              }))
            )
          }
        })
        .catch(err => console.error("Error loading users list", err))
    }
  }, [])

  // Fetch hierarchical data from the API
  const fetchTree = async (currentDepth: number, userId: string | null) => {
    setLoading(true)
    setError("")
    try {
      const response = userId 
        ? await apiService.getUserTree(userId, currentDepth)
        : await apiService.getCurrentUserTree(currentDepth)

      if (response.success && response.data) {
        setUsers(response.data.users)
        setTotalUsers(response.data.totalUsers)
        setMaxDepth(response.data.maxDepth)
      } else {
        setError(response.message || "Não foi possível carregar a árvore de usuários.")
      }
    } catch (err: any) {
      setError(err.message || "Erro ao conectar-se com o servidor.")
    } finally {
      setLoading(false)
    }
  }

  // Reload when depth or selected user changes (clamped between 1 and 100)
  useEffect(() => {
    const numericDepth = typeof depth === "number" ? depth : parseInt(depth, 10)
    if (!isNaN(numericDepth) && numericDepth > 0) {
      fetchTree(numericDepth, selectedUserId)
    }
  }, [depth, selectedUserId])

  // Map user ID to full details for quick O(1) lookup during rendering
  const nodesMap = useMemo(() => {
    const map = new Map<string, UserHierarchyNode>()
    for (const u of users) {
      map.set(u.id, u)
    }
    return map
  }, [users])

  // Build hierarchical Tree data for Mantine from the flat list
  const treeData = useMemo(() => {
    const userIds = new Set(users.map(u => u.id))
    const childrenMap = new Map<string, UserHierarchyNode[]>()
    const roots: UserHierarchyNode[] = []

    for (const u of users) {
      if (!u.parentUserId || !userIds.has(u.parentUserId)) {
        roots.push(u)
      } else {
        const list = childrenMap.get(u.parentUserId) || []
        list.push(u)
        childrenMap.set(u.parentUserId, list)
      }
    }

    const buildNode = (node: UserHierarchyNode): TreeNodeData => {
      const children = childrenMap.get(node.id) || []
      return {
        value: node.id,
        label: node.name,
        children: children.length > 0 ? children.map(buildNode) : undefined
      }
    }

    return roots.map(buildNode)
  }, [users])

  // Mantine tree controller state
  const tree = useTree()

  // Auto-expand nodes or update tree expansion when tree data changes
  useEffect(() => {
    if (treeData.length > 0) {
      // Expand all roots by default
      treeData.forEach(root => {
        tree.expand(root.value)
      })
    }
  }, [treeData])

  // Filter tree nodes that match search criteria
  const matchesSearch = (node: UserHierarchyNode) => {
    if (!search) return false
    const query = search.toLowerCase()
    return (
      node.name.toLowerCase().includes(query) ||
      node.email.toLowerCase().includes(query) ||
      node.role.toLowerCase().includes(query)
    )
  }

  // Custom node renderer with premium layout & interaction
  const renderTreeNode = ({ node, expanded, hasChildren, elementProps }: RenderTreeNodePayload) => {
    const userNode = nodesMap.get(node.value)
    if (!userNode) return null

    const isMatch = matchesSearch(userNode)
    const isInactive = !userNode.isActive

    // Role badge configuration
    const getRoleBadge = (role: string) => {
      switch (role.toLowerCase()) {
        case "superadmin":
          return <Badge size="xs" color="red" variant="filled">Super Admin</Badge>
        case "admin":
          return <Badge size="xs" color="orange" variant="filled">Admin</Badge>
        default:
          return <Badge size="xs" color="blue" variant="light">Usuário</Badge>
      }
    }

    // Avatar color based on role
    const getAvatarColor = (role: string) => {
      switch (role.toLowerCase()) {
        case "superadmin":
          return "red"
        case "admin":
          return "orange"
        default:
          return "blue"
      }
    }

    return (
      <Group 
        gap="xs" 
        {...elementProps} 
        className={`tree-node-group ${isMatch ? "tree-node-highlighted" : ""} ${isInactive ? "tree-node-inactive" : ""}`}
      >
        {hasChildren ? (
          <ActionIcon 
            size="sm" 
            variant="subtle" 
            color="gray"
            onClick={(e) => {
              e.stopPropagation()
              tree.toggleExpanded(node.value)
            }}
          >
            {expanded ? <IconChevronDown size={14} /> : <IconChevronRight size={14} />}
          </ActionIcon>
        ) : (
          <div style={{ width: 22 }} />
        )}

        <Avatar 
          size="sm" 
          radius="xl" 
          color={getAvatarColor(userNode.role)}
        >
          {userNode.name.split(" ").slice(0, 2).map(n => n[0]).join("").toUpperCase()}
        </Avatar>

        <div className="tree-node-content">
          <Group gap="xs" align="center">
            <span className="tree-node-name">{userNode.name}</span>
            {getRoleBadge(userNode.role)}
            {isInactive && <Badge size="xs" color="gray" variant="outline">Inativo</Badge>}
          </Group>
          <span className="tree-node-email">{userNode.email}</span>
        </div>

        <Tooltip label="Visualizar Perfil" position="right" withArrow>
          <ActionIcon
            size="sm"
            variant="subtle"
            color="indigo"
            className="tree-node-action"
            onClick={(e) => {
              e.stopPropagation()
              setSelectedProfileUserId(userNode.id)
            }}
          >
            <IconEye size={16} />
          </ActionIcon>
        </Tooltip>
      </Group>
    )
  }

  return (
    <Menu>
      <div className="tree-page-container">
        <div className="tree-page-header">
          <div>
            <Title order={2} size="h2" className="tree-page-title">
              Árvore de Usuários
            </Title>
            <p className="tree-page-subtitle">
              Visualização hierárquica dos usuários subordinados à sua conta.
            </p>
          </div>
        </div>

        {/* Control bar */}
        <Card withBorder className="tree-controls-card">
          <Group align="flex-end" grow>
            {isSuperAdmin && (
              <Select
                label="Visualizar Árvore de"
                placeholder="Selecione um usuário (Padrão: Minha Árvore)"
                data={availableUsers}
                value={selectedUserId}
                onChange={setSelectedUserId}
                clearable
                searchable
              />
            )}
            <TextInput
              placeholder="Buscar por nome, email ou função..."
              label="Buscar na Árvore"
              leftSection={<IconSearch size={16} />}
              value={search}
              onChange={(e) => setSearch(e.target.value)}
            />
            <NumberInput
              label="Profundidade da Hierarquia"
              value={depth}
              onChange={(val) => setDepth(val || 1)}
              min={1}
              max={100}
              style={{ maxWidth: 200 }}
            />
          </Group>
        </Card>

        {/* Tree display */}
        <Card withBorder className="tree-display-card">
          {error && (
            <div className="error-banner">
              {error}
            </div>
          )}

          {loading ? (
            <div className="tree-loading-container">
              <Loader size="md" color="indigo" />
              <Text size="sm" mt="xs" color="dimmed">
                Carregando hierarquia...
              </Text>
            </div>
          ) : users.length === 0 ? (
            <div className="tree-empty-state">
              <IconTree size={40} className="empty-icon" />
              <Text size="md" fw={500} mt="sm">
                Nenhum usuário na hierarquia
              </Text>
              <Text size="sm" color="dimmed">
                Você não possui usuários abaixo de você nesta árvore.
              </Text>
            </div>
          ) : (
            <div className="tree-scroll-container">
              <div className="tree-stats-bar">
                <Badge color="indigo" leftSection={<IconUsers size={12} />}>
                  {totalUsers} {totalUsers === 1 ? "usuário" : "usuários"} no total
                </Badge>
                <Badge color="gray">
                  Profundidade máxima: {maxDepth}
                </Badge>
              </div>

              <Tree
                data={treeData}
                tree={tree}
                renderNode={renderTreeNode}
              />
            </div>
          )}
        </Card>

        {/* Profile details modal */}
        <UserProfileModal
          userId={selectedProfileUserId}
          opened={selectedProfileUserId !== null}
          onClose={() => setSelectedProfileUserId(null)}
        />
      </div>
    </Menu>
  )
}

export default UserTreePage
