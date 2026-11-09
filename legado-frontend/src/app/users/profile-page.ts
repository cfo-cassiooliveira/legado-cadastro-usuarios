import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { AuthStore } from '../auth/auth-store';
import { initialsOf } from '../shared/initials';
import { formatMobilePhone, formatPostalCode } from '../shared/masks';

const BRAZIL_COUNTRY_CODE = '+55';

@Component({
  selector: 'app-profile-page',
  imports: [DatePipe],
  templateUrl: './profile-page.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProfilePage {
  protected readonly user = inject(AuthStore).currentUser;
  protected readonly initials = computed(() => initialsOf(this.user()?.fullName ?? ''));

  protected formatPhone(e164: string): string {
    return formatMobilePhone(e164.replace(BRAZIL_COUNTRY_CODE, ''));
  }

  protected formatPostalCode(postalCode: string): string {
    return formatPostalCode(postalCode);
  }
}
