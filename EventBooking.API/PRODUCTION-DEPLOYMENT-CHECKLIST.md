# Processing Fee Production Deployment Checklist

## Prerequisites
- [ ] Backend API is running locally and tested
- [ ] Processing fee functionality works in development
- [ ] Database backup is available
- [ ] IIS/Production server access is available

## Database Migration
1. [ ] Connect to production database (kwdb01)
2. [ ] Run the migration script: `processing-fee-migration.sql`
3. [ ] Verify new columns exist in Events table:
   - ProcessingFeePercentage (decimal(5,4))
   - ProcessingFeeFixedAmount (decimal(18,2))
   - ProcessingFeeEnabled (bit)

## Backend API Deployment
1. [ ] Navigate to EventBooking.API directory
2. [ ] Run deployment script: `.\deploy-api-production.ps1`
3. [ ] Verify deployment completed without errors
4. [ ] Check IIS application pool is running

## Post-Deployment Verification
1. [ ] Test API endpoints:
   - [ ] GET `/api/Events` - should work
   - [ ] POST `/api/payment/calculate-processing-fee` - new endpoint
   - [ ] GET `/api/admin/events/{id}/processing-fee` - new endpoint
   - [ ] PUT `/api/admin/events/{id}/processing-fee` - new endpoint

2. [ ] Test Admin Panel:
   - [ ] Login as admin
   - [ ] Go to Events page
   - [ ] Click "Processing Fee" button on any event
   - [ ] Configure processing fee settings
   - [ ] Save and verify

3. [ ] Test Customer Experience:
   - [ ] Go to any event page
   - [ ] Add tickets and proceed to payment
   - [ ] Verify processing fee is calculated and displayed
   - [ ] Check payment summary shows: Subtotal + Processing Fee = Total

## Rollback Plan (if needed)
1. [ ] Restore IIS files from backup: `C:\Backups\EventBookingAPI\[timestamp]`
2. [ ] Restart application pool
3. [ ] Database rollback (remove columns if necessary)

## Success Criteria
- [ ] No 404 errors on frontend
- [ ] Processing fee calculation works
- [ ] Admin can configure processing fees
- [ ] Customers see fee breakdown before payment
- [ ] Payment flow includes processing fees in total

## Notes
- Frontend is already deployed with processing fee UI
- Backend now needs to match the frontend functionality
- Test thoroughly before marking as complete
