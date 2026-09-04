import { AnimationKey } from '../types/Notification';

export interface AnimationAsset {
  key: AnimationKey;
  path: string;
  loop: boolean;
  name: string;
}

/**
 * Registry mapping abstract animation keys from notifications to client-side Lottie JSON files.
 * The backend only sends the string key (e.g. 'LEVEL_UP', 'TROPHY'), never executable or hardcoded animations.
 */
export const ANIMATION_MAP: Record<AnimationKey, AnimationAsset | null> = {
  NONE: null,
  LEVEL_UP: {
    key: 'LEVEL_UP',
    path: '/animations/level_up.json',
    loop: false,
    name: 'Subiu de Nível'
  },
  BADGE_UNLOCKED: {
    key: 'BADGE_UNLOCKED',
    path: '/animations/badge_unlocked.json',
    loop: false,
    name: 'Nova Conquista'
  },
  TROPHY: {
    key: 'TROPHY',
    path: '/animations/trophy.json',
    loop: false,
    name: 'Troféu'
  },
  TARGET_REACHED: {
    key: 'TARGET_REACHED',
    path: '/animations/target_reached.json',
    loop: false,
    name: 'Meta Atingida'
  },
  URGENT_ALERT: {
    key: 'URGENT_ALERT',
    path: '/animations/urgent_alert.json',
    loop: true,
    name: 'Alerta Urgente'
  }
};
