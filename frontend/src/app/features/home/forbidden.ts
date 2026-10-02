import { Component } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-forbidden',
  imports: [MatButtonModule, RouterLink],
  template: `
    <h1>Access denied</h1>
    <p>Your role does not give access to this page.</p>
    <a mat-stroked-button routerLink="/">Back to home</a>
  `,
})
export class Forbidden {}
