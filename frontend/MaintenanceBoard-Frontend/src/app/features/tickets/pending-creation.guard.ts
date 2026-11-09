import { CanDeactivateFn } from '@angular/router';

/** Keep an in-flight creation request alive when the user navigates within the app. */
export const pendingCreationGuard: CanDeactivateFn<{ saving: () => boolean }> = component => !component.saving();
