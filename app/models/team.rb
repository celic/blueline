# == Schema Information
#
# Table name: teams
#
#  id           :integer          not null, primary key
#  name         :string
#  abbreviation :string
#  city         :string
#  state        :string
#  full_name    :string
#

class Team < ActiveRecord::Base

	## Callbacks
	before_create :generate_full_name

	## Private Methods
	private
	def generate_full_name

		# set default full name if not set
		self.full_name ||= "#{self.city} #{self.name}"

		# continue with creation
		true
	end
end