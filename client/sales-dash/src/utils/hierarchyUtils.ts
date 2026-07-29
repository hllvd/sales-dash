import { User } from '../services/apiService';

/**
 * Efficiently retrieves all descendant (subordinate) users for a given parent user ID using an O(N) adjacency-list BFS traversal.
 * 
 * @param parentUserId ID of the root parent user
 * @param allUsers Array of all available users in the system/context
 * @returns Array of User objects that are descendants of the parent user
 */
export function getSubordinateUsers(parentUserId: string | null | undefined, allUsers: User[]): User[] {
  if (!parentUserId || !allUsers || allUsers.length === 0) return [];

  // Single-pass O(N) adjacency list construction
  const childrenMap = new Map<string, User[]>();
  for (let i = 0; i < allUsers.length; i++) {
    const user = allUsers[i];
    if (user.parentUserId) {
      let list = childrenMap.get(user.parentUserId);
      if (!list) {
        list = [];
        childrenMap.set(user.parentUserId, list);
      }
      list.push(user);
    }
  }

  // BFS traversal using queue
  const visited = new Set<string>([parentUserId]);
  const queue: string[] = [parentUserId];
  const result: User[] = [];

  while (queue.length > 0) {
    const currentId = queue.shift()!;
    const children = childrenMap.get(currentId);
    if (children) {
      for (let i = 0; i < children.length; i++) {
        const child = children[i];
        if (!visited.has(child.id)) {
          visited.add(child.id);
          queue.push(child.id);
          result.push(child);
        }
      }
    }
  }

  return result;
}

/**
 * Efficiently retrieves a Set of all descendant (subordinate) user IDs for a given parent user ID.
 * 
 * @param parentUserId ID of the root parent user
 * @param allUsers Array of all available users in the system/context
 * @returns Set of user ID strings representing all descendants
 */
export function getSubordinateUserIdsSet(parentUserId: string | null | undefined, allUsers: User[]): Set<string> {
  const subordinates = getSubordinateUsers(parentUserId, allUsers);
  return new Set(subordinates.map(u => u.id));
}
