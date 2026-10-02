import { TestBed } from '@angular/core/testing';
import providers from './test-providers';

// The Karma runner has no "providersFile": register the shared providers before every spec instead.
beforeEach(() => TestBed.configureTestingModule({ providers }));
