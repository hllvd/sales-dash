import React, { ReactNode } from 'react'

interface FormFieldProps {
  label: string
  required?: boolean
  children: ReactNode
  description?: string
}

/**
 * Reusable form field component for modals with consistent styling
 */
const FormField: React.FC<FormFieldProps> = ({ 
  label, 
  required = false, 
  children,
  description 
}) => {
  // Check if children is a checkbox by checking if it's a React element with type.displayName or type.name containing 'Checkbox'
  const isCheckbox = React.isValidElement(children) && 
    (children.type?.toString().includes('Checkbox') || 
     (typeof children.props === 'object' && children.props !== null && 'checked' in children.props));

  return (
    <div style={{ marginBottom: '1rem' }}>
      {isCheckbox ? (
        // Inline layout for checkboxes
        <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
          {children}
          <label style={{ 
            fontSize: '14px', 
            fontWeight: 500,
            color: 'white',
            cursor: 'pointer'
          }}>
            {label} {required && <span style={{ color: 'red' }}>*</span>}
          </label>
        </div>
      ) : (
        // Block layout for other inputs
        <>
          <label style={{ 
            display: 'block', 
            marginBottom: '0.25rem', 
            fontSize: '14px', 
            fontWeight: 500,
            color: 'white'
          }}>
            {label} {required && <span style={{ color: 'red' }}>*</span>}
          </label>
          {children}
        </>
      )}
      {description && (
        <div style={{ 
          fontSize: '12px', 
          color: '#a0a0a0', 
          marginTop: '0.25rem' 
        }}>
          {description}
        </div>
      )}
    </div>
  )
}

export default FormField
