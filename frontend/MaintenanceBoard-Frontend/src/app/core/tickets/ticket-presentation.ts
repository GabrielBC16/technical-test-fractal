/** Build real initials for both the signed-in user and ticket assignees. */
export function initials(name: string): string {
  return name.trim().split(/\s+/).slice(0, 2).map(part => part[0] ?? '').join('').toLocaleUpperCase('es');
}
