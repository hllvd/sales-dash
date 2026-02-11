import React, { ReactNode } from 'react'

interface FormFieldProps {
  label: string
  required?: boolean
  children: ReactNode
  description?: string
  labelColor?: string
}

/**
 * Reusable form field component for modals with consistent styling
 * Optimized for dark backgrounds with high-contrast text
 */
const FormField: React.FC<FormFieldProps> = ({ 
  label, 
  required = false, 
  children,
  description,
  labelColor = '#e9ecef'
}) => {
  // Check if children is a checkbox by checking if it's a React element with type.displayName or type.name containing 'Checkbox'
  const isCheckbox = React.isValidElement(children) && 
    (children.type?.toString().includes('Checkbox') || 
     (typeof children.props === 'object' && children.props !== null && 'checked' in children.props));

  return (
    <div style={{ marginBottom: '1.25rem' }}>
      {isCheckbox ? (
        // Inline layout for checkboxes
        <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
          {children}
          <label style={{ 
            fontSize: '14px', 
            fontWeight: 500,
            color: labelColor,
            cursor: 'pointer'
          }}>
            {label} {required && <span style={{ color: '#fa5252' }}>*</span>}
          </label>
        </div>
      ) : (
        // Block layout for other inputs
        <>
          <label style={{ 
            display: 'block', 
            marginBottom: '0.5rem', 
            fontSize: '14px', 
            fontWeight: 600,
            color: labelColor,
            letterSpacing: '0.01em'
          }}>
            {label} {required && <span style={{ color: '#fa5252' }}>*</span>}
          </label>
          {children}
        </>
      )}
      {description && (
        <div style={{ 
          fontSize: '12px', 
          color: '#adb5bd', 
          marginTop: '0.375rem',
          lineHeight: '1.4'
        }}>
          {description}
        </div>
      )}
    </div>
  )
}

export default FormField
