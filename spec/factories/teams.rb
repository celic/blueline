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
#  division_id  :integer
#  wins         :integer          default(0)
#  losses       :integer          default(0)
#  ot           :integer          default(0)
#  so           :integer          default(0)
#  points       :integer          default(0)
#
# Indexes
#
#  index_teams_on_abbreviation  (abbreviation)
#

FactoryGirl.define do
  factory :team do
    
  end

end
