import { bootstrapApplication } from '@angular/platform-browser';
import { appConfig } from './app/app.config';
import { App } from './app/app';
import { applyDocumentLanguage, initialLanguage } from './app/core/i18n/language';

applyDocumentLanguage(initialLanguage());

bootstrapApplication(App, appConfig)
  .catch((err) => console.error(err));
