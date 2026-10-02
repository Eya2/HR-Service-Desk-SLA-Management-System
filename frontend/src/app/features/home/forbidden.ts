import { Component } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';

@Component({
  selector: 'app-forbidden',
  imports: [MatButtonModule, RouterLink, TranslatePipe],
  template: `
    <h1>{{ 'home.forbiddenTitle' | translate }}</h1>
    <p>{{ 'home.forbiddenText' | translate }}</p>
    <a mat-stroked-button routerLink="/">{{ 'home.backHome' | translate }}</a>
  `,
})
export class Forbidden {}
