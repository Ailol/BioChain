export type AppRole = 'private' | 'work' | 'both' | 'worker' | 'admin';

export function expandEffectiveRoles(roles: string[]): string[] {
  const effective = new Set(roles);
  if (effective.has('both')) {
    effective.add('work');
    effective.add('private');
  }
  if (effective.has('admin')) {
    effective.add('work');
    effective.add('private');
    effective.add('both');
    effective.add('worker');
  }
  return Array.from(effective);
}

export function hasRole(effectiveRoles: string[], required: string): boolean {
  return effectiveRoles.includes(required);
}

export function hasAnyRole(effectiveRoles: string[], required: string[]): boolean {
  return required.some(r => effectiveRoles.includes(r));
}
