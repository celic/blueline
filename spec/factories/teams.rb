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
# Indexes
#
#  index_teams_on_abbreviation  (abbreviation)
#

FactoryGirl.define do
  factory :team do
    
  end

end
