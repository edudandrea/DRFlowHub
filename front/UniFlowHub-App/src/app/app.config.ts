import { ApplicationConfig, importProvidersFrom, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideHttpClient, withFetch, withInterceptors } from '@angular/common/http';
import { provideRouter } from '@angular/router';
import { provideAnimations } from '@angular/platform-browser/animations';
import { provideToastr } from 'ngx-toastr';
import { provideSpinnerConfig } from 'ngx-spinner';
import { ModalModule } from 'ngx-bootstrap/modal';

import { routes } from './app.routes';
import { provideClientHydration, withEventReplay } from '@angular/platform-browser';
import { authInterceptor } from './core/auth.interceptor';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes),
    provideHttpClient(withFetch(), withInterceptors([authInterceptor])),
    provideClientHydration(withEventReplay()),
    provideAnimations(),
    importProvidersFrom(ModalModule),
    provideToastr({
      closeButton: true,
      progressBar: true,
      preventDuplicates: true,
      positionClass: 'toast-bottom-right',
      timeOut: 3500,
    }),
    provideSpinnerConfig({ type: 'ball-scale-multiple' }),
  ]
};
