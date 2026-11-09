import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { finalize } from 'rxjs';
import { apiErrorMessage } from '../../core/api/api-error';
import { SessionService } from '../../core/auth/session.service';

@Component({
  selector: 'app-login',
  imports: [ReactiveFormsModule],
  templateUrl: './login.html',
})
export class Login {
  private readonly session = inject(SessionService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly expired = inject(ActivatedRoute).snapshot.queryParamMap.get('expired') === '1';
  protected readonly busy = signal(false);
  protected readonly error = signal('');
  protected readonly form = inject(FormBuilder).nonNullable.group({
    userName: ['', [Validators.required, Validators.maxLength(100), Validators.pattern(/\S/)]],
    password: ['', [Validators.required, Validators.maxLength(1024)]],
  });

  /** Normalize the user name, preserve the password, and prevent duplicate requests. */
  protected submit(): void {
    if (this.busy()) return;
    this.form.markAllAsTouched();
    if (this.form.invalid) return;
    this.error.set('');
    this.busy.set(true);
    const value = this.form.getRawValue();
    this.session.login({ ...value, userName: value.userName.trim() }).pipe(
      takeUntilDestroyed(this.destroyRef),
      finalize(() => this.busy.set(false)),
    ).subscribe({
      next: () => {
        this.form.controls.password.reset();
        void this.router.navigateByUrl('/board');
      },
      error: (error: unknown) => this.error.set(apiErrorMessage(error, true)),
    });
  }
}
