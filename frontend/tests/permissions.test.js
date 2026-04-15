import { describe, it, expect } from 'vitest';
import { buildPermissions } from '../src/shared/utils/permissions';

describe('Permissions', () => {
  it('should grant loan officer appropriate permissions', () => {
    const p = buildPermissions('LoanOfficer');
    expect(p.createCustomer).toBe(true);
    expect(p.createApplication).toBe(true);
    expect(p.approveApplication).toBe(false);
    expect(p.disburse).toBe(true);
    expect(p.recordPayment).toBe(true);
    expect(p.addCollectionNote).toBe(false);
  });

  it('should grant admin all permissions', () => {
    const p = buildPermissions('Admin');
    expect(p.createCustomer).toBe(true);
    expect(p.approveApplication).toBe(true);
    expect(p.addCollectionNote).toBe(true);
    expect(p.manageProducts).toBe(true);
  });

  it('should grant approver ONLY approval permissions', () => {
    const p = buildPermissions('Approver');
    expect(p.createCustomer).toBe(false);
    expect(p.createApplication).toBe(false);
    expect(p.approveApplication).toBe(true);
    expect(p.disburse).toBe(false);
  });
});
