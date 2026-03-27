using System;
using System.Collections.Generic;
using System.Text;

namespace DynamicFormBuilder.Tests.Factories
{
    public static class SubmissionTestFactory
    {
        public static FormSubmission CreatePendingExternalSubmission()
        {
            return new FormSubmission
            {
                Id = "submission-1",
                CreatedByUserId = "owner-1",
                Status = SubmissionStatus.Pending,
                RowVersion = 2,
                FieldsSnapshot = new List<FieldDefinition>
            {
                new() { FieldId = "owner-signature", Label = "Hire Team Member", Type = "Signature", AssignedTo = AssignedTo.You, Required = true },
                new() { FieldId = "client-name", Label = "Full Name", Type = "ShortText", AssignedTo = AssignedTo.Client, Required = false },
                new() { FieldId = "client-email", Label = "Email", Type = "Email", AssignedTo = AssignedTo.Client, Required = true },
                new() { FieldId = "client-signature", Label = "Job Applicant", Type = "Signature", AssignedTo = AssignedTo.Client, Required = true }
            },
                Answers = new List<FormAnswer>
            {
                new() { FieldId = "owner-signature", Value = "/uploads/owner.png" },
                new() { FieldId = "client-name", Value = "Old Name" },
                new() { FieldId = "client-email", Value = "old@mail.com" },
                new() { FieldId = "client-signature", Value = "/uploads/client-old.png" }
            },
                Signatures = new List<FormSignature>()
            };
        }
    }
}
